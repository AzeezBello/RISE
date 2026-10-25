using UnityEngine;
using UnityEngine.InputSystem;

namespace Rise
{
    // Input through the Input System package. The action maps in Resources/Input/Rise.inputactions carry the
    // keyboard + mouse and gamepad bindings (the Gamepad layout covers DualShock, Xbox and Switch controllers, which is
    // what console ports rebind); when the asset is missing the same accessors poll the devices directly. Every accessor
    // is null-safe for headless runs.
    //
    //   Move      WASD / arrows, left stick          Look      mouse, right stick, Z / C
    //   Sprint    Shift, Square / X button           Jump      Space, Triangle / Y button
    //   Interact  E, Cross / A button                Decline   X, Backspace, Circle / B button
    //   Attack    F, Left Click (locked), RB / R1    Pickup    G, D-pad down / E on ground item
    //   Weapon    Q, LB / L1 (cycle equipped item)
    //   Driving   W/S or triggers for gas and brake, A/D or left stick to steer, Shift boost, Space handbrake
    //   Phone     P, Tab, Select                     Chat      T, Enter, D-pad up       Menu / cursor   Esc, Start
    public static class GameInput
    {
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        static Gamepad G => Gamepad.current;
        static InputActionAsset asset; static InputActionMap map, drivingMap, uiMap; static bool loaded;
        static InputAction move, look, sprint, jump, interact, decline, phone, chat, menu, gas, brake, handbrake, attack, pickup, cycleWeapon, horn;
        static InputAction driveSteer, driveThrottle, driveBrake, driveHandbrake, driveBoost, driveHorn;
        static InputAction uiNavigate, uiSubmit, uiCancel, uiTabLeft, uiTabRight;

        public static bool UsingActions { get { Load(); return map != null; } }
        public static bool HasDrivingMap { get { Load(); return drivingMap != null; } }
        public static bool HasUIMap { get { Load(); return uiMap != null; } }
        public static int ControlSchemeCount { get { Load(); return asset != null ? asset.controlSchemes.Count : 0; } }

        public static string ControllerFamily
        {
            get
            {
                if (UI.TouchControls.Active) return "Touch";
                var g = G;
                if (g != null)
                {
                    var desc = (g.description.interfaceName + " " + g.description.product + " " + g.name).ToLowerInvariant();
                    if (desc.Contains("dualshock") || desc.Contains("dualsense") || desc.Contains("playstation") || desc.Contains("ps4") || desc.Contains("ps5")) return "PlayStation";
                    if (desc.Contains("switch") || desc.Contains("nintendo") || desc.Contains("joycon")) return "Switch";
                    if (desc.Contains("xinput") || desc.Contains("xbox")) return "Xbox";
                    return "Gamepad";
                }
                return "Keyboard";
            }
        }

        static void Load()
        {
            if (loaded) return; loaded = true;
            asset = Resources.Load<InputActionAsset>("Input/Rise");
            if (asset == null) return;
            map = asset.FindActionMap("Gameplay");
            if (map != null)
            {
                move = map.FindAction("Move"); look = map.FindAction("Look"); sprint = map.FindAction("Sprint"); jump = map.FindAction("Jump"); interact = map.FindAction("Interact"); decline = map.FindAction("Decline");
                phone = map.FindAction("Phone"); chat = map.FindAction("Chat"); menu = map.FindAction("Menu"); gas = map.FindAction("Gas"); brake = map.FindAction("Brake"); handbrake = map.FindAction("Handbrake");
                attack = map.FindAction("Attack"); pickup = map.FindAction("Pickup"); cycleWeapon = map.FindAction("CycleWeapon"); horn = map.FindAction("Horn");
                map.Enable();
            }
            drivingMap = asset.FindActionMap("Driving");
            if (drivingMap != null)
            {
                driveSteer = drivingMap.FindAction("Steer"); driveThrottle = drivingMap.FindAction("Throttle"); driveBrake = drivingMap.FindAction("Brake");
                driveHandbrake = drivingMap.FindAction("Handbrake"); driveBoost = drivingMap.FindAction("Boost"); driveHorn = drivingMap.FindAction("Horn");
                drivingMap.Enable();
            }
            uiMap = asset.FindActionMap("UI");
            if (uiMap != null)
            {
                uiNavigate = uiMap.FindAction("Navigate"); uiSubmit = uiMap.FindAction("Submit"); uiCancel = uiMap.FindAction("Cancel");
                uiTabLeft = uiMap.FindAction("TabLeft"); uiTabRight = uiMap.FindAction("TabRight");
                uiMap.Enable();
            }
        }

        public static Vector2 Move
        {
            get
            {
                Load();
                if (UI.TouchControls.Active && UI.TouchControls.I.Move.sqrMagnitude > 0f) return UI.TouchControls.I.Move;
                if (move != null) return Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
                var v = Vector2.zero;
                if (K != null)
                {
                    if (K.aKey.isPressed || K.leftArrowKey.isPressed) v.x -= 1f;
                    if (K.dKey.isPressed || K.rightArrowKey.isPressed) v.x += 1f;
                    if (K.wKey.isPressed || K.upArrowKey.isPressed) v.y += 1f;
                    if (K.sKey.isPressed || K.downArrowKey.isPressed) v.y -= 1f;
                }
                if (G != null && v == Vector2.zero) v = G.leftStick.ReadValue();
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        // In the legacy "Mouse X" scale (pixels × 0.1); the right stick is folded in at about 120°/s at full tilt.
        public static Vector2 LookDelta
        {
            get
            {
                Load();
                var touchLook = UI.TouchControls.TakeLook();
                if (look != null)
                {
                    var l = look.ReadValue<Vector2>();
                    bool fromMouse = look.activeControl != null && look.activeControl.device is Mouse;
                    if (fromMouse) return (Cursor.lockState == CursorLockMode.Locked ? l : Vector2.zero) + touchLook;
                    return l * new Vector2(34f, 33f) * Time.deltaTime + touchLook;   // stick: per-second rate
                }
                var d = Vector2.zero;
                if (M != null && Cursor.lockState == CursorLockMode.Locked) d = M.delta.ReadValue() * 0.1f;
                if (G != null) d += G.rightStick.ReadValue() * new Vector2(55f, 40f) * Time.deltaTime;
                return d + touchLook;
            }
        }

        static bool Pressed(InputAction a, bool fallback) { Load(); return a != null ? a.WasPressedThisFrame() : fallback; }
        static bool Held(InputAction a, bool fallback) { Load(); return a != null ? a.IsPressed() : fallback; }
        static float Axis(InputAction a, float fallback) { Load(); return a != null ? Mathf.Clamp01(a.ReadValue<float>()) : fallback; }

        public static bool LookLeftKey => K != null && K.zKey.isPressed;
        public static bool LookRightKey => K != null && K.cKey.isPressed;
        public static bool Sprint => (UI.TouchControls.Active && UI.TouchControls.I.SprintHeld) || Held(sprint, (K != null && (K.leftShiftKey.isPressed || K.rightShiftKey.isPressed)) || (G != null && G.buttonWest.isPressed)) || Held(driveBoost, false);
        public static bool JumpPressed => UI.TouchControls.TakeJump() || Pressed(jump, (K != null && K.spaceKey.wasPressedThisFrame) || (G != null && G.buttonNorth.wasPressedThisFrame));
        public static bool InteractPressed => UI.TouchControls.TakeInteract() || Pressed(interact, (K != null && K.eKey.wasPressedThisFrame) || (G != null && G.buttonSouth.wasPressedThisFrame)) || Pressed(uiSubmit, false);
        public static bool DeclinePressed => UI.TouchControls.TakeDecline() || Pressed(decline, (K != null && (K.xKey.wasPressedThisFrame || K.backspaceKey.wasPressedThisFrame)) || (G != null && G.buttonEast.wasPressedThisFrame)) || Pressed(uiCancel, false);
        public static bool Handbrake => (UI.TouchControls.Active && UI.TouchControls.I.HandbrakeHeld) || Held(handbrake, (K != null && K.spaceKey.isPressed) || (G != null && G.buttonEast.isPressed)) || Held(driveHandbrake, false);
        public static float Gas => Mathf.Max(Mathf.Max(Axis(gas, G != null ? G.rightTrigger.ReadValue() : 0f), Axis(driveThrottle, 0f)), Mathf.Max(0f, Move.y));
        public static float Brake => Mathf.Max(Mathf.Max(Axis(brake, G != null ? G.leftTrigger.ReadValue() : 0f), Axis(driveBrake, 0f)), Mathf.Max(0f, -Move.y));
        public static float Steer
        {
            get
            {
                Load();
                float s = Move.x;
                if (driveSteer != null) { float ds = driveSteer.ReadValue<float>(); if (Mathf.Abs(ds) > Mathf.Abs(s)) s = ds; }
                return Mathf.Clamp(s, -1f, 1f);
            }
        }
        public static Vector2 MenuNavigate
        {
            get
            {
                Load();
                if (uiNavigate != null)
                {
                    var nav = uiNavigate.ReadValue<Vector2>();
                    if (nav.sqrMagnitude > 0.04f) return Vector2.ClampMagnitude(nav, 1f);
                }
                if (G != null)
                {
                    var dpad = G.dpad.ReadValue();
                    if (dpad.sqrMagnitude > 0.04f) return Vector2.ClampMagnitude(dpad, 1f);
                }
                return Move;
            }
        }
        public static bool HornPressed => Pressed(horn, (K != null && K.hKey.wasPressedThisFrame) || (G != null && G.leftStickButton.wasPressedThisFrame)) || Pressed(driveHorn, false);
        public static bool EscapePressed => Pressed(menu, (K != null && K.escapeKey.wasPressedThisFrame) || (G != null && G.startButton.wasPressedThisFrame));
        public static bool PausePressed => EscapePressed || (K != null && K.f1Key.wasPressedThisFrame);
        public static bool MapPressed => K != null && K.mKey.wasPressedThisFrame;
        public static bool ClickPressed => M != null && M.leftButton.wasPressedThisFrame;
        public static bool ChatPressed => Pressed(chat, K != null && (K.tKey.wasPressedThisFrame || K.enterKey.wasPressedThisFrame));
        public static bool PhonePressed => UI.TouchControls.TakePhone() || Pressed(phone, (K != null && (K.pKey.wasPressedThisFrame || K.tabKey.wasPressedThisFrame)) || (G != null && G.selectButton.wasPressedThisFrame));
        public static bool AttackPressed
        {
            get
            {
                Load();
                if (UI.TouchControls.TakeFight()) return true;
                if (K != null && K.fKey.wasPressedThisFrame) return true;
                if (G != null && G.rightShoulder.wasPressedThisFrame) return true;
                if (M != null && Cursor.lockState == CursorLockMode.Locked && M.leftButton.wasPressedThisFrame) return true;
                if (attack != null && attack.WasPressedThisFrame())
                {
                    if (attack.activeControl != null && attack.activeControl.device is Mouse && Cursor.lockState != CursorLockMode.Locked) return false;
                    return true;
                }
                return false;
            }
        }
        public static bool AttackHeld
        {
            get
            {
                if (K != null && K.fKey.isPressed) return true;
                if (G != null && G.rightShoulder.isPressed) return true;
                if (M != null && Cursor.lockState == CursorLockMode.Locked && M.leftButton.isPressed) return true;
                return false;
            }
        }
        public static bool ReloadPressed => K != null && K.rKey.wasPressedThisFrame;
        public static bool PickupPressed => UI.TouchControls.TakeItem() || Pressed(pickup, (K != null && K.gKey.wasPressedThisFrame) || (G != null && G.dpad.down.wasPressedThisFrame));
        public static bool CycleWeaponPressed => Pressed(cycleWeapon, (K != null && K.qKey.wasPressedThisFrame) || (G != null && G.leftShoulder.wasPressedThisFrame));

        // A tapped menu row (touch), or -1.
        public static int MenuTap => UI.TouchControls.TakeMenuTap();

        // Mouse wheel vertical scroll for long menus.
        public static float MenuScrollDelta => M != null ? M.scroll.ReadValue().y : 0f;

        // 1–9 on the keyboard for menu options, 0 when none was pressed this frame.
        public static int DigitPressed
        {
            get
            {
                if (K == null) return 0;
                if (K.digit1Key.wasPressedThisFrame) return 1; if (K.digit2Key.wasPressedThisFrame) return 2; if (K.digit3Key.wasPressedThisFrame) return 3;
                if (K.digit4Key.wasPressedThisFrame) return 4; if (K.digit5Key.wasPressedThisFrame) return 5; if (K.digit6Key.wasPressedThisFrame) return 6;
                if (K.digit7Key.wasPressedThisFrame) return 7; if (K.digit8Key.wasPressedThisFrame) return 8; if (K.digit9Key.wasPressedThisFrame) return 9;
                return 0;
            }
        }

        // True if any key, mouse button, gamepad button, or touchscreen press was triggered this frame.
        public static bool AnyPressed
        {
            get
            {
                if (K != null && K.anyKey.wasPressedThisFrame) return true;
                if (M != null && (M.leftButton.wasPressedThisFrame || M.rightButton.wasPressedThisFrame || M.middleButton.wasPressedThisFrame)) return true;
                if (G != null && (G.buttonSouth.wasPressedThisFrame || G.buttonEast.wasPressedThisFrame || G.buttonNorth.wasPressedThisFrame || G.buttonWest.wasPressedThisFrame || G.startButton.wasPressedThisFrame || G.selectButton.wasPressedThisFrame)) return true;
                if (Touchscreen.current != null && (Touchscreen.current.primaryTouch.press.wasPressedThisFrame || Touchscreen.current.primaryTouch.press.isPressed)) return true;
                return false;
            }
        }
    }
}
