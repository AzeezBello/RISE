#!/usr/bin/env node
// Unified local & CI build automation runner for the Unity 6 project in unity/.
// Usage:
//   node scripts/build-unity.mjs <target> [--skip-export] [--skip-zip] [--dry-run]
//
// Targets:
//   compile             Fast headless Roslyn C# compile check (Assembly-CSharp + Assembly-CSharp-Editor)
//   smoke               Headless Edit-Mode SmokeTest (Rise.EditorTools.SceneSetup.SmokeTest)
//   test | playtest     Headless Play-Mode PlayTest suite (Rise.EditorTools.PlayTest.Run)
//   validate            Validate all 10 platform build targets (Rise.EditorTools.SceneSetup.ValidatePlatformTargets)
//   webgl               Build WebGL player (Builds/WebGL) and package WebGL.zip
//   mac | macos         Build macOS Universal player (Builds/Rise.app) and package Rise-macOS.zip
//   windows | win64     Build Windows 64-bit player (Builds/Windows/Rise.exe)
//   ios                 Build iOS Xcode project (Builds/iOS)
//   android             Build Android APK (Builds/Android/Rise.apk)
//   all                 Run export -> compile -> smoke -> test -> validate -> webgl -> mac -> windows -> ios -> android

import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const unityDir = join(rootDir, 'unity');
const logsDir = join(unityDir, 'Logs');
const buildsDir = join(unityDir, 'Builds');

const args = process.argv.slice(2);
const flags = new Set(args.filter(a => a.startsWith('--')));
const targets = args.filter(a => !a.startsWith('--'));
const target = (targets[0] || 'all').toLowerCase();

const skipExport = flags.has('--skip-export');
const skipZip = flags.has('--skip-zip');
const dryRun = flags.has('--dry-run');

function readProjectUnityVersion() {
  const versionFile = join(unityDir, 'ProjectSettings', 'ProjectVersion.txt');
  if (existsSync(versionFile)) {
    const m = readFileSync(versionFile, 'utf8').match(/m_EditorVersion:\s*(\S+)/);
    if (m) return m[1];
  }
  return '6000.6.5f1';
}

function findUnityBinary() {
  if (process.env.UNITY_PATH && existsSync(process.env.UNITY_PATH)) return process.env.UNITY_PATH;
  if (process.env.UNITY && existsSync(process.env.UNITY)) return process.env.UNITY;

  const version = readProjectUnityVersion();
  const candidates = [
    `/Applications/Unity/Hub/Editor/${version}/Unity.app/Contents/MacOS/Unity`,
    `C:\\Program Files\\Unity\\Hub\\Editor\\${version}\\Editor\\Unity.exe`,
    `/opt/unity/Editor/Unity`,
    `${process.env.HOME || ''}/Unity/Hub/Editor/${version}/Editor/Unity`,
  ];
  for (const c of candidates) {
    if (existsSync(c)) return c;
  }

  // Fallback: scan /Applications/Unity/Hub/Editor for any 6000.* version
  const macHub = '/Applications/Unity/Hub/Editor';
  if (existsSync(macHub)) {
    const dirs = readdirSync(macHub).sort().reverse();
    for (const d of dirs) {
      const bin = join(macHub, d, 'Unity.app', 'Contents', 'MacOS', 'Unity');
      if (existsSync(bin)) return bin;
    }
  }
  return null;
}

function findUnityContentsDir(unityBin) {
  if (!unityBin) return null;
  // macOS: .../Unity.app/Contents/MacOS/Unity -> .../Unity.app/Contents
  const macContents = resolve(dirname(unityBin), '..');
  if (existsSync(join(macContents, 'Resources', 'Scripting'))) return macContents;
  // Linux/Windows: .../Editor/Data
  const dataDir = join(dirname(unityBin), 'Data');
  if (existsSync(join(dataDir, 'Resources', 'Scripting'))) return dataDir;
  return null;
}

const PLATFORM_MATRIX = [
  { name: 'macOS Universal', target: 'StandaloneOSX', module: 'MacStandaloneSupport', output: 'Builds/Rise.app', method: 'Rise.EditorTools.SceneSetup.BuildMac' },
  { name: 'Windows 64-bit', target: 'StandaloneWindows64', module: 'WindowsStandaloneSupport', output: 'Builds/Windows/Rise.exe', method: 'Rise.EditorTools.SceneSetup.BuildWindows' },
  { name: 'WebGL (Gzip)', target: 'WebGL', module: 'WebGLSupport', output: 'Builds/WebGL', method: 'Rise.EditorTools.SceneSetup.BuildWebGL' },
  { name: 'iOS (Xcode)', target: 'iOS', module: 'iOSSupport', output: 'Builds/iOS', method: 'Rise.EditorTools.SceneSetup.BuildiOS' },
  { name: 'Android (ARM64)', target: 'Android', module: 'AndroidPlayer', output: 'Builds/Android/Rise.apk', method: 'Rise.EditorTools.SceneSetup.BuildAndroid' },
  { name: 'PlayStation 5', target: 'PS5', module: 'PS5Player', output: 'Builds/PS5', method: 'Rise.EditorTools.SceneSetup.BuildPS5' },
  { name: 'PlayStation 4', target: 'PS4', module: 'PS4Player', output: 'Builds/PS4', method: 'Rise.EditorTools.SceneSetup.BuildPS4' },
  { name: 'Xbox Series X|S', target: 'GameCoreXboxSeries', module: 'GameCoreScarlettSupport', output: 'Builds/XboxSeries', method: 'Rise.EditorTools.SceneSetup.BuildXboxSeries' },
  { name: 'Xbox One', target: 'GameCoreXboxOne', module: 'GameCoreXboxOneSupport', output: 'Builds/XboxOne', method: 'Rise.EditorTools.SceneSetup.BuildXboxOne' },
  { name: 'Nintendo Switch', target: 'Switch', module: 'Switch', output: 'Builds/Switch/Rise.nsp', method: 'Rise.EditorTools.SceneSetup.BuildSwitch' },
];

function validatePlatformMatrix(unityBin) {
  console.log(`[build-unity] Unity Editor: ${unityBin || 'not found'} (project version ${readProjectUnityVersion()})`);
  for (const p of PLATFORM_MATRIX) {
    const installed = isPlaybackModuleInstalled(unityBin, p.module);
    const outExists = existsSync(join(unityDir, p.output));
    console.log(
      `  [${p.name.padEnd(16)}] target=${p.target.padEnd(20)} module=${installed ? 'INSTALLED' : 'missing  '} built=${outExists ? 'YES' : 'no '} -> unity/${p.output}`
    );
  }
}

function isPlaybackModuleInstalled(unityBin, moduleFolder) {
  if (!unityBin) return false;
  // macOS Hub layout: /Applications/Unity/Hub/Editor/<ver>/PlaybackEngines/<moduleFolder>
  const editorRoot = resolve(dirname(unityBin), '../../..');
  const enginePath = join(editorRoot, 'PlaybackEngines', moduleFolder);
  if (existsSync(enginePath)) return true;
  // Linux/Windows layout: .../Editor/Data/PlaybackEngines/<moduleFolder>
  const altPath = join(dirname(unityBin), 'Data', 'PlaybackEngines', moduleFolder);
  return existsSync(altPath);
}

function ensureWorldExport() {
  if (skipExport) return;
  console.log('[build-unity] Syncing world data (scripts/export-world.mjs)...');
  if (dryRun) return;
  execFileSync(process.execPath, [join(rootDir, 'scripts', 'export-world.mjs')], {
    cwd: rootDir,
    stdio: 'inherit',
  });
}

function findFilesRecursive(dir, ext) {
  const results = [];
  if (!existsSync(dir)) return results;
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) results.push(...findFilesRecursive(full, ext));
    else if (entry.name.endsWith(ext)) results.push(full);
  }
  return results;
}

function parseCsproj(csprojPath) {
  const xml = readFileSync(csprojPath, 'utf8');
  const definesMatch = xml.match(/<DefineConstants>([^<]+)<\/DefineConstants>/);
  const defines = definesMatch ? definesMatch[1].split(';').map(s => s.trim()).filter(Boolean) : [];
  const refs = [];
  const hintRegex = /<HintPath>([^<]+)<\/HintPath>/g;
  let m;
  while ((m = hintRegex.exec(xml)) !== null) {
    const p = m[1].startsWith('/') || m[1].includes(':') ? m[1] : join(unityDir, m[1]);
    if (existsSync(p)) refs.push(p);
  }
  return { defines, refs };
}

function runRoslynCompile(unityBin) {
  const t0 = Date.now();
  const contentsDir = findUnityContentsDir(unityBin);
  if (!contentsDir) {
    throw new Error('Cannot locate Unity Scripting directory for headless C# compilation.');
  }

  const dotnetBin = join(contentsDir, 'Resources', 'Scripting', 'NetCoreRuntime', 'dotnet');
  const sdkRoot = join(contentsDir, 'Resources', 'Scripting', 'DotNetSdk', 'sdk');
  const sdkVer = existsSync(sdkRoot) ? readdirSync(sdkRoot).find(d => !d.startsWith('.')) : null;
  const cscDll = sdkVer ? join(sdkRoot, sdkVer, 'Roslyn', 'bincore', 'csc.dll') : null;

  if (!existsSync(dotnetBin) || !cscDll || !existsSync(cscDll)) {
    throw new Error(`Bundled Roslyn compiler not found at ${cscDll}`);
  }

  const outDir = join(unityDir, 'Temp', 'CompileCheck');
  mkdirSync(outDir, { recursive: true });

  const runtimeCsproj = join(unityDir, 'Assembly-CSharp.csproj');
  const editorCsproj = join(unityDir, 'Assembly-CSharp-Editor.csproj');

  let { defines, refs } = existsSync(runtimeCsproj)
    ? parseCsproj(runtimeCsproj)
    : { defines: ['UNITY_6000', 'UNITY_EDITOR', 'ENABLE_INPUT_SYSTEM'], refs: [] };

  // Also include netstandard2.1 facade references required by csc when /nostdlib is set
  const netstandardRefDir = join(
    contentsDir,
    'Resources',
    'Scripting',
    'BCLExtensions',
    'TargetingPacks',
    'netstandard2.1',
    'ref',
    'netstandard2.1'
  );
  if (existsSync(netstandardRefDir)) {
    for (const f of readdirSync(netstandardRefDir)) {
      if (f.endsWith('.dll')) refs.push(join(netstandardRefDir, f));
    }
  }
  if (refs.length === 0) {
    const managedEngineDir = join(contentsDir, 'Resources', 'Scripting', 'Managed', 'UnityEngine');
    if (existsSync(managedEngineDir)) {
      for (const f of readdirSync(managedEngineDir)) {
        if (f.endsWith('.dll')) refs.push(join(managedEngineDir, f));
      }
    }
    const scriptAssembliesDir = join(unityDir, 'Library', 'ScriptAssemblies');
    if (existsSync(scriptAssembliesDir)) {
      for (const f of readdirSync(scriptAssembliesDir)) {
        if (f.endsWith('.dll') && !f.startsWith('Assembly-CSharp')) {
          refs.push(join(scriptAssembliesDir, f));
        }
      }
    }
  }

  const uniqueRefs = [...new Set(refs)];
  const runtimeScripts = findFilesRecursive(join(unityDir, 'Assets', 'Rise', 'Scripts'), '.cs');
  const runtimeDll = join(outDir, 'Assembly-CSharp.dll');

  const runtimeRsp = [
    '-target:library',
    `-out:"${runtimeDll}"`,
    '-langversion:9.0',
    '-nostdlib',
    '-nowarn:0169,0649,USG0001',
    ...defines.map(d => `-define:${d}`),
    ...uniqueRefs.map(r => `-r:"${r}"`),
    ...runtimeScripts.map(s => `"${s}"`),
  ].join('\n');

  const runtimeRspPath = join(outDir, 'runtime.rsp');
  writeFileSync(runtimeRspPath, runtimeRsp);

  if (dryRun) {
    console.log(`[build-unity] [dry-run] Would compile ${runtimeScripts.length} runtime scripts + editor scripts via Roslyn`);
    return;
  }

  execFileSync(dotnetBin, [cscDll, '-nologo', '-noconfig', `@${runtimeRspPath}`], { cwd: unityDir, stdio: 'inherit' });

  const editorScripts = findFilesRecursive(join(unityDir, 'Assets', 'Rise', 'Editor'), '.cs');
  const editorDefines = existsSync(editorCsproj)
    ? parseCsproj(editorCsproj).defines
    : [...defines, 'UNITY_EDITOR'];
  const editorDll = join(outDir, 'Assembly-CSharp-Editor.dll');

  const editorRsp = [
    '-target:library',
    `-out:"${editorDll}"`,
    '-langversion:9.0',
    '-nostdlib',
    '-nowarn:0169,0649,USG0001',
    ...editorDefines.map(d => `-define:${d}`),
    ...uniqueRefs.map(r => `-r:"${r}"`),
    `-r:"${runtimeDll}"`,
    ...editorScripts.map(s => `"${s}"`),
  ].join('\n');

  const editorRspPath = join(outDir, 'editor.rsp');
  writeFileSync(editorRspPath, editorRsp);
  execFileSync(dotnetBin, [cscDll, '-nologo', '-noconfig', `@${editorRspPath}`], { cwd: unityDir, stdio: 'inherit' });

  const elapsed = ((Date.now() - t0) / 1000).toFixed(2);
  console.log(
    `[build-unity] C# compile PASS (${runtimeScripts.length} runtime + ${editorScripts.length} editor scripts, 0 errors in ${elapsed}s)`
  );
}

function isUnityProjectLocked() {
  const lockfile = join(unityDir, 'Temp', 'UnityLockfile');
  if (!existsSync(lockfile)) return false;
  try {
    const out = execFileSync('pgrep', ['-f', '--', `-projectPath.*${unityDir}`], { encoding: 'utf8' }).trim();
    return out.length > 0;
  } catch {
    return false;
  }
}

function runUnityBatch({ label, executeMethod, quit = true, nographics = true, extraArgs = [], expectPattern = null }) {
  const unityBin = findUnityBinary();
  if (!unityBin) {
    throw new Error(
      'Unity Editor binary not found. Set UNITY_PATH or install Unity 6000.6.5f1 in /Applications/Unity/Hub/Editor/.'
    );
  }

  mkdirSync(logsDir, { recursive: true });
  const logFile = join(logsDir, `build-${label}.log`);

  const cliArgs = ['-batchmode'];
  if (quit) cliArgs.push('-quit');
  if (nographics) cliArgs.push('-nographics');
  cliArgs.push('-projectPath', unityDir, '-executeMethod', executeMethod, '-logFile', logFile, ...extraArgs);

  console.log(`[build-unity] Running ${label}: "${unityBin}" ${cliArgs.join(' ')}`);
  if (dryRun) return { logFile, output: '' };

  if (isUnityProjectLocked()) {
    throw new Error(
      `Unity project at ${unityDir} is currently open in another Unity Editor instance. Close Unity or run 'npm run unity:compile' for a lock-free C# check.`
    );
  }

  const childEnv = { ...process.env };
  if (process.platform !== 'win32') {
    delete childEnv.USERPROFILE;
  }

  const t0 = Date.now();
  return new Promise((resolvePromise, rejectPromise) => {
    const child = spawn(unityBin, cliArgs, { cwd: rootDir, env: childEnv, stdio: ['ignore', 'pipe', 'pipe'] });
    child.stdout.on('data', d => process.stdout.write(d));
    child.stderr.on('data', d => process.stderr.write(d));
    child.on('error', err => rejectPromise(err));
    child.on('close', code => {
      const elapsed = ((Date.now() - t0) / 1000).toFixed(1);
      const output = existsSync(logFile) ? readFileSync(logFile, 'utf8') : '';
      const highlights = output
        .split(/\r?\n/)
        .filter(line => /^(RISE |PlayTest|error CS|CompilerOutput|Exception)/i.test(line));
      for (const h of highlights) console.log(`  ${h}`);

      if (code !== 0) {
        rejectPromise(new Error(`[build-unity] ${label} exited with code ${code} after ${elapsed}s (see ${logFile})`));
        return;
      }
      if (expectPattern && !expectPattern.test(output)) {
        rejectPromise(
          new Error(`[build-unity] ${label} did not match expected pattern ${expectPattern} (see ${logFile})`)
        );
        return;
      }
      console.log(`[build-unity] ${label} completed in ${elapsed}s (log: ${logFile})`);
      resolvePromise({ logFile, output });
    });
  });
}

function zipArtifact(sourcePath, zipDestPath, cwdDir) {
  if (skipZip || dryRun || !existsSync(sourcePath)) return;
  if (existsSync(zipDestPath)) rmSync(zipDestPath, { force: true });
  const relName = sourcePath.slice(cwdDir.length + 1);
  console.log(`[build-unity] Packaging ${zipDestPath}...`);
  execFileSync('zip', ['-r', '-q', '-y', zipDestPath, relName, '-x', '*.DS_Store'], {
    cwd: cwdDir,
    stdio: 'inherit',
  });
  const sizeMb = (statSync(zipDestPath).size / (1024 * 1024)).toFixed(1);
  console.log(`[build-unity] Packaged ${zipDestPath} (${sizeMb} MB)`);
}

async function runTarget(t) {
  const unityBin = findUnityBinary();
  switch (t) {
    case 'compile': {
      runRoslynCompile(unityBin);
      break;
    }
    case 'smoke': {
      ensureWorldExport();
      await runUnityBatch({
        label: 'smoke',
        executeMethod: 'Rise.EditorTools.SceneSetup.SmokeTest',
        quit: true,
        nographics: true,
        expectPattern: /RISE smoke test: PASS/,
      });
      break;
    }
    case 'test':
    case 'playtest': {
      ensureWorldExport();
      await runUnityBatch({
        label: 'playtest',
        executeMethod: 'Rise.EditorTools.PlayTest.Run',
        quit: false,
        nographics: true,
        expectPattern: /RISE play test: PASS \(\d+ checks|ALL \d+ CHECKS PASSED/,
      });
      break;
    }
    case 'validate': {
      ensureWorldExport();
      validatePlatformMatrix(unityBin);
      runRoslynCompile(unityBin);
      if (flags.has('--unity-batch')) {
        await runUnityBatch({
          label: 'validate-targets',
          executeMethod: 'Rise.EditorTools.SceneSetup.ValidatePlatformTargets',
          quit: true,
          nographics: true,
          expectPattern: /RISE platform target/,
        });
      }
      break;
    }
    case 'webgl': {
      ensureWorldExport();
      if (!isPlaybackModuleInstalled(unityBin, 'WebGLSupport')) {
        console.warn('[build-unity] WebGLSupport playback engine not installed; skipping WebGL player build.');
        return;
      }
      await runUnityBatch({
        label: 'webgl',
        executeMethod: 'Rise.EditorTools.SceneSetup.BuildWebGL',
        quit: true,
        nographics: true,
        expectPattern: /RISE: WebGL build Succeeded/,
      });
      if (!dryRun) {
        execFileSync(process.execPath, [join(rootDir, 'scripts', 'package-webgl.mjs')], {
          cwd: rootDir,
          stdio: 'inherit',
        });
      }
      break;
    }
    case 'mac':
    case 'macos': {
      ensureWorldExport();
      if (!isPlaybackModuleInstalled(unityBin, 'MacStandaloneSupport')) {
        console.warn('[build-unity] MacStandaloneSupport playback engine not installed; skipping macOS build.');
        return;
      }
      await runUnityBatch({
        label: 'macos',
        executeMethod: 'Rise.EditorTools.SceneSetup.BuildMac',
        quit: true,
        nographics: true,
        expectPattern: /RISE: StandaloneOSX build Succeeded/,
      });
      zipArtifact(
        join(buildsDir, 'Rise.app'),
        join(buildsDir, 'Rise-macOS.zip'),
        buildsDir
      );
      break;
    }
    case 'windows':
    case 'win64': {
      ensureWorldExport();
      if (!isPlaybackModuleInstalled(unityBin, 'WindowsStandaloneSupport')) {
        console.log(
          '[build-unity] WindowsStandaloneSupport module not installed locally; validating target configuration via Unity...'
        );
      }
      await runUnityBatch({
        label: 'windows',
        executeMethod: 'Rise.EditorTools.SceneSetup.BuildWindows',
        quit: true,
        nographics: true,
      });
      if (existsSync(join(buildsDir, 'Windows', 'Rise.exe'))) {
        zipArtifact(
          join(buildsDir, 'Windows'),
          join(buildsDir, 'Rise-Windows.zip'),
          buildsDir
        );
      }
      break;
    }
    case 'ios': {
      ensureWorldExport();
      if (!isPlaybackModuleInstalled(unityBin, 'iOSSupport')) {
        console.log('[build-unity] iOSSupport module not installed locally; validating target configuration via Unity...');
      }
      await runUnityBatch({
        label: 'ios',
        executeMethod: 'Rise.EditorTools.SceneSetup.BuildiOS',
        quit: true,
        nographics: true,
      });
      break;
    }
    case 'android': {
      ensureWorldExport();
      if (!isPlaybackModuleInstalled(unityBin, 'AndroidPlayer')) {
        console.log(
          '[build-unity] AndroidPlayer module not installed locally; validating target configuration via Unity...'
        );
      }
      await runUnityBatch({
        label: 'android',
        executeMethod: 'Rise.EditorTools.SceneSetup.BuildAndroid',
        quit: true,
        nographics: true,
      });
      break;
    }
    case 'all': {
      ensureWorldExport();
      runRoslynCompile(unityBin);
      await runTarget('smoke');
      await runTarget('playtest');
      await runTarget('validate');
      await runTarget('webgl');
      await runTarget('mac');
      await runTarget('windows');
      await runTarget('ios');
      await runTarget('android');
      break;
    }
    default:
      throw new Error(
        `Unknown build target "${t}". Expected one of: compile, smoke, test, validate, webgl, mac, windows, ios, android, all`
      );
  }
}

try {
  await runTarget(target);
} catch (err) {
  console.error(err.message || err);
  process.exit(1);
}
