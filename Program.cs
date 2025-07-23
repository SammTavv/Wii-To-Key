using System.Diagnostics;
using WiimoteLib;
using WindowsInput;
using WindowsInput.Native;

class Program
{
    private static Wiimote wm;
    private static InputSimulator inputSimulator = new();

    private static List<string> files;
    private static List<string> fileNames;
    private static string filePath = Directory.CreateDirectory(Directory.GetCurrentDirectory() + @"\Mappings\") + "default.wiimap";
    private static int index = 0;

    private static Dictionary<Button, List<(VirtualKeyCode Key, bool Holding)>> keyMappings;
    private static Dictionary<Button, (bool IsPressed, bool Holding)> buttonStates = new();

    public static double sensitivityX = 40;
    public static double sensitivityY = 20;
    public static double sensitivityX2 = 80;
    public static double sensitivityY2 = 10;
    public static double deadzoneX = 0.05;
    public static double deadzoneY = 0.09;
    public static List<Button> scrollKeys = [];
    public static List<Button> modeKeys = [];
    public static bool showConfigs = true;
    public static bool debug = false;
    public static bool mousePressKey = false;
    public static bool cancelPressed = false;
    public static bool useTiltSteering = false;
    public static List<Button> stopKeys = [];
    public static List<Button> steerKeys = [];

    private const int screenWidth = 1920;
    private const int screenHeight = 1080;
    private const double maxJoystickValue = 0.5;
    private static double lastX = screenWidth / 2;
    private static double lastY = screenHeight / 2;

    /// <summary>
    /// Holds every Wiimote (+ Nunchuck) button
    /// </summary>
    public enum Button
    {
        Right,
        Left,
        Up,
        Down,
        Minus,
        Plus,
        A,
        B,
        Home,
        One,
        Two,
        C,
        Z,
        Right2,
        Left2,
        Up2,
        Down2,
    }

    static void Main(string[] args)
    {
        foreach (Button button in Enum.GetValues(typeof(Button)))
        {
            buttonStates[button] = (false, false);
        }

        files = new List<string>();
        fileNames = new List<string>();

        InitDefMap();
        LoadFiles();
        WriteFiles();

        try
        {
            LoadKeyMappings(files[index]);
        }
        catch
        {
            RestartProgram();
        }

        bool restart = false;

        do
        {
            restart = false;
            wm = new();

            try
            {
                wm.Connect();
                LoadConfigs("configs.txt");

                if (showConfigs)
                {
                    ShowConfigs();
                }

                wm.WiimoteChanged += wm_WiimoteChanged;
                var ws = wm.WiimoteState;
                wm.SetReportType(InputReport.IRAccel, IRSensitivity.Maximum, true);

                Console.WriteLine($"Loaded mapping: {fileNames[index]} ({index + 1} of {fileNames.Count})\n");

                Console.WriteLine($"Program is running\nPress any Wii Remote button...\nOpen the \"README.txt\" file for instructions");

                if (debug) Console.WriteLine($"" +
                    $"\nExtension: {wm.WiimoteState.ExtensionType}" +
                    $"\n\nl1: {wm.WiimoteState.LEDState.LED1}, l2: {wm.WiimoteState.LEDState.LED2}, l3: {wm.WiimoteState.LEDState.LED3}, l4: {wm.WiimoteState.LEDState.LED4}" +
                    $"\n\nBattery: {wm.WiimoteState.Battery}" +
                    $"\nDevice path: {wm.HIDDevicePath}" +
                    $"\nID: {wm.ID}"
                    );

                while (true)
                {
                    string input = Console.ReadLine();

                    if (input.Contains("\\r"))
                    {
                        RestartProgram();
                        break;
                    }
                    else if (input.Contains("\\c"))
                    {
                        Console.Clear();
                        File.WriteAllText(files[index], "");
                        File.WriteAllText("configs.txt", defaultConfigs);

                        RestartProgram();
                        break;
                    }
                    else if (input.Contains("\\w"))
                    {
                        Console.Clear();
                        Console.WriteLine("Writing mode entered, type any \"Button = KEY\" pair\nType \"\\r\" to update the file");
                        RunWritingMode(ref restart);

                        if (restart)
                        {
                            LoadKeyMappings(files[index]);
                            RestartProgram();
                        }
                    }
                    else if (input.Contains("\\n"))
                    {
                        index = (index + 1) % files.Count;
                        LoadKeyMappings(files[index]);

                        RestartConsoleOnly();
                    }
                    else if (input.Contains("\\p"))
                    {
                        index = (index - 1) % 1;
                        LoadKeyMappings(files[index]);

                        RestartConsoleOnly();
                    }
                    else if (input.Contains("\\o"))
                    {
                        ProcessStartInfo psi = new()
                        {
                            FileName = files[index],
                            UseShellExecute = true
                        };

                        Process.Start(psi);
                    }
                    else if (input.Contains("\\x"))
                    {
                        ProcessStartInfo psi = new()
                        {
                            FileName = "configs.txt",
                            UseShellExecute = true
                        };

                        Process.Start(psi);
                    }
                    else if (input.Contains("\\i"))
                    {
                        ProcessStartInfo psi = new()
                        {
                            FileName = "infos.txt",
                            UseShellExecute = true
                        };

                        Process.Start(psi);
                    }
                }
            }
            catch (WiimoteNotFoundException)
            {
                Console.Clear();
                Console.WriteLine("Connect the Wiimote to start the program\nCheck \"README.txt\" for a tutorial");
                //RunDvcInst();
                Console.ReadLine();
            }
            catch
            {
                wm.WiimoteChanged -= wm_WiimoteChanged;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Clear();
                Console.WriteLine("Program Stopped");
                Console.ResetColor();
                Console.WriteLine("Type \"\\r\" to restart");
            }
        }
        while (restart);
    }

    private static string defaultConfigs = $"" +
        $"Mouse Sensitivity X: {sensitivityX}" +
        $"\nMouse Sensitivity Y: {sensitivityY}" +
        $"\nSteer Sensitivity X: {sensitivityX2}" +
        $"\nSteer Sensitivity Y: {sensitivityY2}" +
        $"\nDeadzone X: {deadzoneX}" +
        $"\nDeadzone Y: {deadzoneY}" +
        $"\nMouse Scroll: C, Z" +
        $"\nJoystick Keyboard: Plus, Minus" +
        $"\nShow: true" +
        $"\nDebug: false" +
        $"\nStop Program: A, B, Home" +
        $"\nSteering Wheel: Minus, Left";

    private static void ShowConfigs()
    {
        Console.WriteLine("Current Configs:");
        Console.WriteLine($"- Mouse Sensitivity X: {sensitivityX}");
        Console.WriteLine($"- Mouse Sensitivity Y: {sensitivityY}");
        Console.WriteLine($"- Steer Sensitivity X: {sensitivityX2}");
        Console.WriteLine($"- Steer Sensitivity Y: {sensitivityY2}");
        Console.WriteLine($"- Deadzone X: {deadzoneX}");
        Console.WriteLine($"- Deadzone Y: {deadzoneY}");
        Console.WriteLine($"- Mouse Scroll: {string.Join(", ", scrollKeys)}");
        Console.WriteLine($"- Joystick Keyboard: {string.Join(", ", modeKeys)}");
        Console.WriteLine($"- Stop Program: {string.Join(", ", stopKeys)}");
        Console.WriteLine($"- Steering Wheel: {string.Join(", ", steerKeys)}");
    }

    public static void RunDvcInst()
    {
        ProcessStartInfo processInfo = new ProcessStartInfo("control.exe", "printers")
        {
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            Process.Start(processInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes every console string without updating any file (extremely reduntant but i'm lazy af)
    /// </summary>
    static void RestartConsoleOnly()
    {
        Console.Clear();

        if (showConfigs)
        {
            ShowConfigs();
        }

        Console.WriteLine($"Loaded mapping: {fileNames[index]} ({index + 1} of {fileNames.Count})\n");

        Console.WriteLine($"Program is running\nPress any Wii Remote button...\nOpen the \"README.txt\" file for instructions");

        if (debug) Console.WriteLine($"" +
                    $"\nExtension: {wm.WiimoteState.ExtensionType}" +
                    $"\n\nl1:{wm.WiimoteState.LEDState.LED1}, l2:{wm.WiimoteState.LEDState.LED2}, l3:{wm.WiimoteState.LEDState.LED3}, l4:{wm.WiimoteState.LEDState.LED4}" +
                    $"\n\nBattery: {wm.WiimoteState.Battery}" +
                    $"\nDevice path:{wm.HIDDevicePath}" +
                    $"\nID: {wm.ID}"
                    );

    }

    /// <summary>
    /// Closes the current environment and starts a copy of itself
    /// </summary>
    static void RestartProgram()
    {
        Console.Clear();
        Process.Start(Environment.ProcessPath);
        Environment.Exit(0);
    }

    /// <summary>
    /// Event called every frame the Wiimote sends a report
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    static void wm_WiimoteChanged(object sender, WiimoteChangedEventArgs e)
    {
        WiimoteState ws = e.WiimoteState;

        CheckButtonState(Button.Right, ws.ButtonState.Right);
        CheckButtonState(Button.Left, ws.ButtonState.Left);
        CheckButtonState(Button.Up, ws.ButtonState.Up);
        CheckButtonState(Button.Down, ws.ButtonState.Down);
        CheckButtonState(Button.Minus, ws.ButtonState.Minus);
        CheckButtonState(Button.Plus, ws.ButtonState.Plus);
        CheckButtonState(Button.A, ws.ButtonState.A);
        CheckButtonState(Button.B, ws.ButtonState.B);
        CheckButtonState(Button.Home, ws.ButtonState.Home);
        CheckButtonState(Button.One, ws.ButtonState.One);
        CheckButtonState(Button.Two, ws.ButtonState.Two);
        CheckButtonState(Button.C, ws.NunchukState.C);
        CheckButtonState(Button.Z, ws.NunchukState.Z);

        UpdateMouse(ws);

        if (AllButtonsPressed(ws, modeKeys))
        {
            if (!cancelPressed)
            {
                mousePressKey = !mousePressKey;
                cancelPressed = true;
            }
        }
        else
        {
            cancelPressed = false;
        }

        if (AllButtonsPressed(ws, stopKeys))
        {
            wm.WiimoteChanged -= wm_WiimoteChanged;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Clear();
            Console.WriteLine("Program Stopped");
            Console.ResetColor();
            Console.WriteLine("Type \"\\r\" to restart");
        }

        if (AllButtonsPressed(ws, steerKeys))
        {
            useTiltSteering = !useTiltSteering;
        }
    }
    /// <summary>
    /// Holds the mouse movement, scrolling and directional-key-pressing functions
    /// </summary>
    /// <param name="ws"></param>
    static void UpdateMouse(WiimoteState ws)
    {
        if (useTiltSteering)
        {
            double roll = (ws.AccelState.Values.Z - 0.5) * 2;
            double pitch = ws.AccelState.Values.Y * 1;

            if (Math.Abs(roll) < deadzoneY) roll = 0;
            if (Math.Abs(pitch) < deadzoneX) pitch = 0;

            double moveX = pitch * sensitivityX2;
            double moveY = roll * sensitivityY2;

            lastX += moveX;
            lastY += moveY;

            lastX = Math.Clamp(lastX, 0, screenWidth - 1);
            lastY = Math.Clamp(lastY, 0, screenHeight - 1);

            double scaledX = (lastX / screenWidth) * 65535.0;
            double scaledY = (lastY / screenHeight) * 65535.0;

            //inputSimulator.Mouse.MoveMouseToPositionOnVirtualDesktop(scaledX, scaledY);
            inputSimulator.Mouse.MoveMouseBy((int)moveX, (int)moveY);
        }
        else
        {
            double joystickX = ws.NunchukState.Joystick.X;
            double joystickY = ws.NunchukState.Joystick.Y;

            if (Math.Abs(joystickX) < deadzoneX)
            {
                joystickX = 0;
            }
            if (Math.Abs(joystickY) < deadzoneY)
            {
                joystickY = 0;
            }

            if (AllButtonsPressed(ws, scrollKeys))
            {
                if (joystickY > 0.2)
                {
                    inputSimulator.Mouse.VerticalScroll(1);
                }
                else if (joystickY < -0.2)
                {
                    inputSimulator.Mouse.VerticalScroll(-1);
                }
                else if (joystickX > 0.2)
                {
                    inputSimulator.Mouse.HorizontalScroll(1);
                }
                else if (joystickX < -0.2)
                {
                    inputSimulator.Mouse.HorizontalScroll(-1);
                }
            }
            else if (mousePressKey)
            {
                HandleDirectionalKeyPresses(joystickX, joystickY);
            }
            else
            {
                joystickY = -joystickY;

                int deltaX = (int)(joystickX / sensitivityX * (screenWidth / 2));
                int deltaY = (int)(joystickY / sensitivityY * (screenHeight / 2));

                lastX += deltaX;
                lastY -= deltaY;

                lastX = Math.Max(0, Math.Min(screenWidth, lastX));
                lastY = Math.Max(0, Math.Min(screenHeight, lastY));

                inputSimulator.Mouse.MoveMouseBy(deltaX, deltaY);
            }
        }
    }


    private static bool isKeyPressedUp = false;
    private static bool isKeyPressedDown = false;
    private static bool isKeyPressedLeft = false;
    private static bool isKeyPressedRight = false;
    private static DateTime lastDirectionReleaseTime = DateTime.Now;

    /// <summary>
    /// Presses buttons based on the Nunchuk's <paramref name="joystickX"/> position and <paramref name="joystickY"/>
    /// </summary>
    /// <param name="joystickX"></param>
    /// <param name="joystickY"></param>
    static void HandleDirectionalKeyPresses(double joystickX, double joystickY)
    {
        double angle = Math.Atan2(joystickY, joystickX) * (180 / Math.PI);
        bool joystickInMotion = Math.Abs(joystickX) > 0.2 || Math.Abs(joystickY) > 0.2;

        if (joystickInMotion)
        {
            if (angle >= -22.5 && angle < 22.5) PressDirection(ref isKeyPressedRight, Button.Right2);
            else if (angle >= 22.5 && angle < 67.5) PressDiagonal(ref isKeyPressedUp, ref isKeyPressedRight, Button.Up2, Button.Right2);
            else if (angle >= 67.5 && angle < 112.5) PressDirection(ref isKeyPressedUp, Button.Up2);
            else if (angle >= 112.5 && angle < 157.5) PressDiagonal(ref isKeyPressedUp, ref isKeyPressedLeft, Button.Up2, Button.Left2);
            else if ((angle >= 157.5 && angle <= 180) || (angle < -157.5 && angle >= -180)) PressDirection(ref isKeyPressedLeft, Button.Left2);
            else if (angle >= -157.5 && angle < -112.5) PressDiagonal(ref isKeyPressedDown, ref isKeyPressedLeft, Button.Down2, Button.Left2);
            else if (angle >= -112.5 && angle < -67.5) PressDirection(ref isKeyPressedDown, Button.Down2);
            else if (angle >= -67.5 && angle < -22.5) PressDiagonal(ref isKeyPressedDown, ref isKeyPressedRight, Button.Down2, Button.Right2);
        }
        else
        {
            if ((DateTime.Now - lastDirectionReleaseTime).TotalMilliseconds > 100)
            {
                ReleaseAllDirections();
                lastDirectionReleaseTime = DateTime.Now;
            }
        }
    }

    static void PressDirection(ref bool isKeyPressed, Button button)
    {
        if (!isKeyPressed)
        {
            PressKey(button);
            ResetDirectionFlags();
            isKeyPressed = true;
        }
    }

    static void PressDiagonal(ref bool isKeyPressedFirst, ref bool isKeyPressedSecond, Button buttonFirst, Button buttonSecond)
    {
        if (!isKeyPressedFirst || !isKeyPressedSecond)
        {
            PressKey(buttonFirst);
            PressKey(buttonSecond);
            ResetDirectionFlags();
            isKeyPressedFirst = true;
            isKeyPressedSecond = true;
        }
    }

    static void ReleaseAllDirections()
    {
        if (isKeyPressedUp || isKeyPressedDown || isKeyPressedLeft || isKeyPressedRight)
        {
            ReleaseKey(Button.Up2);
            ReleaseKey(Button.Down2);
            ReleaseKey(Button.Left2);
            ReleaseKey(Button.Right2);

            isKeyPressedUp = false;
            isKeyPressedDown = false;
            isKeyPressedLeft = false;
            isKeyPressedRight = false;
        }
    }

    static void ResetDirectionFlags()
    {
        isKeyPressedUp = false;
        isKeyPressedDown = false;
        isKeyPressedLeft = false;
        isKeyPressedRight = false;
    }
    /// <summary>
    /// Checks if all buttons contained in <paramref name="buttons"/> are pressed
    /// </summary>
    /// <param name="ws"></param>
    /// <param name="buttons">The list that holds the available pressable buttons</param>
    /// <returns></returns>
    static bool AllButtonsPressed(WiimoteState ws, List<Button> buttons)
    {
        foreach (var button in buttons)
        {
            bool isPressed = false;

            switch (button)
            {
                case Button.C:
                    isPressed = ws.NunchukState.C;
                    break;
                case Button.Z:
                    isPressed = ws.NunchukState.Z;
                    break;
                case Button.A:
                    isPressed = ws.ButtonState.A;
                    break;
                case Button.B:
                    isPressed = ws.ButtonState.B;
                    break;
                case Button.One:
                    isPressed = ws.ButtonState.One;
                    break;
                case Button.Two:
                    isPressed = ws.ButtonState.Two;
                    break;
                case Button.Plus:
                    isPressed = ws.ButtonState.Plus;
                    break;
                case Button.Minus:
                    isPressed = ws.ButtonState.Minus;
                    break;
                case Button.Home:
                    isPressed = ws.ButtonState.Home;
                    break;
                case Button.Up:
                    isPressed = ws.ButtonState.Up;
                    break;
                case Button.Down:
                    isPressed = ws.ButtonState.Down;
                    break;
                case Button.Left:
                    isPressed = ws.ButtonState.Left;
                    break;
                case Button.Right:
                    isPressed = ws.ButtonState.Right;
                    break;
            }

            if (!isPressed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Saves every <see cref="Console.ReadLine()"/> in the mapping file when <paramref name="restart"/> is called
    /// </summary>
    /// <param name="restart"></param>
    static void RunWritingMode(ref bool restart)
    {
        bool read = true;
        var newLines = new List<string>(File.ReadAllLines(filePath));

        while (read)
        {
            string input = Console.ReadLine();

            if (input.Contains("\\r"))
            {
                restart = true;
                break;
            }

            var inputParts = input.Split('=', 2);

            if (inputParts.Length != 2)
            {
                continue;
            }

            string inputButton = inputParts[0].Trim();
            string inputKey = inputParts[1].Trim();

            bool lineExists = false;

            for (int i = 0; i < newLines.Count; i++)
            {
                var lineParts = newLines[i].Split(new[] { '=' }, 2);

                if (lineParts.Length == 2)
                {
                    string fileButton = lineParts[0].Trim();

                    if (fileButton.Equals(inputButton, StringComparison.OrdinalIgnoreCase))
                    {
                        newLines[i] = $"{fileButton} = {inputKey}";
                        lineExists = true;
                        break;
                    }
                }
            }

            if (!lineExists)
            {
                newLines.Add(input);
            }

            Console.Clear();
            Console.WriteLine("Writing mode entered, type any \"Button = KEY\" pair\nType \"\\r\" to update the file");
        }

        File.WriteAllLines(filePath, newLines);
    }

    /// <summary>
    /// Handles key presses based on <paramref name="button"/>'s state
    /// </summary>
    /// <param name="button"></param>
    /// <param name="isPressed"></param>
    static void CheckButtonState(Button button, bool isPressed)
    {
        if (keyMappings.ContainsKey(button))
        {
            var (wasPressed, holding) = buttonStates[button];

            if (holding)
            {
                if (isPressed && !wasPressed)
                {
                    PressKey(button);
                }
                else if (!isPressed && wasPressed)
                {
                    ReleaseKey(button);
                    buttonStates[button] = (false, holding);
                }
            }
            else
            {
                if (isPressed && !wasPressed)
                {
                    PressKey(button);
                    buttonStates[button] = (true, holding);
                }
                else if (!isPressed && wasPressed)
                {
                    ReleaseKey(button);
                    buttonStates[button] = (false, holding);
                }
            }
        }
    }

    /// <summary>
    /// Initializes and writes informations to 3 different files
    /// </summary>
    public static void WriteFiles()
    {
        var VirtualKeyCodeArray = Enum.GetValues(typeof(VirtualKeyCode)).Cast<VirtualKeyCode>();
        string keycodes = string.Join("\n", VirtualKeyCodeArray.Select(k => $"{k}"));

        var WiiButtonArray = Enum.GetValues(typeof(Button)).Cast<Button>();
        string buttons = string.Join("\n", WiiButtonArray.Select(k => $"{k}"));

        File.WriteAllText("infos.txt", $"" +
            $"Buttons:" +
            $"\n{buttons}" +
            $"\n\nKeys:" +
            $"\n{keycodes}");

        File.WriteAllText("README.txt", "" +
            "How to use:" +
            "\n- Connect your Wii remote via Bluetooth" +
            "\n- Open the \"keymap.txt\" file" +
            "\n- Map buttons to keys" +
            "\n- Run the \".exe\" application" +
            "\n(Optional: If you have one, use your nunchuk's joystick to move the mouse)" +
            "" +
            "\n\nHow to connect a Wii remote:" +
            "\n- Open the \"Control Panel\" application" +
            "\n- Right click on the \"Devices and Printers\" section" +
            "\n- Click the \"Open in new window\" button" +
            "\n- Click the \"Add a device\" button" +
            "\n- Press the small red button on the back of your Wii remote (in the battery case)" +
            "\n- Select the \"Nintendo RVL-CNT-01\" device" +
            "\n- Click \"Next\" without writing any passcode" +
            "\n- Wait for the remote to connect" +
            "\n(Optional: hold the power button to disconnect the remote)" +
            "" +
            "\n\nHow to map buttons to keys:" +
            "\n- Create a new \".wiimap\" file inside of the \"Mappings\" folder, or edit the default one" +
            "\n- Write the desired button followed by the \"=\" sign" +
            "\n- Write the desired key/s to press (multiple keys must be separated by a comma)" +
            "\n(Optional: open the \"info.txt\" file to view the list of available buttons and keys)" +
            "" +
            "\n\nExample Button-to-KEY mapping:" +
            "\nDown = VK_D" +
            "\nA = VK_H, VK_I" +
            "" +
            "\n\nConfigs:" +
            "\n- Mouse Sensitivity X: the mouse speed for the X axis" +
            "\n- Mouse Sensitivity Y: the mouse speed for the Y axis" +
            "\n- Joystick Deadzone: the amount of ignored small movements of the joystick" +
            "\n- Mouse Scroll: key/s that toggle mouse scrolling when held" +
            "\n- Joystick Keyboard: key/s that toggle the joystick keyboard function:" +
            "\npresses mappable \"Up2\", \"Right2\", \"Down2\" or \"Left2\" buttons based on the joystick's angle" +
            "\n- Show: whether to show the configs on starting the program or not" +
            "\n- Debug: logs debug info" +
            "\n- Use Steering: moves the mouse based on the Wiimote's pitch and roll (horizontal)" +
            "\n- Stop Program: key/s that stop the program" +
            "\n(Configs are found in the \"configs.txt\" file)" +
            "" +
            "\n\nConsole commands:" +
            "\n\\r: restarts the program, updating every file" +
            "\n\\w: lets you add/update any Button-to-KEY mapping directly from the console" +
            "\n\\c: resets the current mapping and config file" +
            "\n\\n: switches to the next available mapping file" +
            "\n\\p: switches to the previous available mapping file" +
            "\n\\o: opens the current mapping file" +
            "\n\\x: opens the configs file" +
            "\n\\i: opens this file");

        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, $"");
        }

        if (!File.Exists("configs.txt"))
        {
            File.WriteAllText("configs.txt", defaultConfigs);
        }
    }

    /// <summary>
    /// Loads the key mappings from <paramref name="filePath"/> and adds the available ones in a <see cref="Dictionary{TKey, TValue}"/> for the program to read from
    /// </summary>
    /// <param name="filePath"></param>
    static void LoadKeyMappings(string filePath)
    {
        keyMappings = new Dictionary<Button, List<(VirtualKeyCode Key, bool Holding)>>();
        buttonStates.Clear();

        string[] lines = File.ReadAllLines(filePath);

        foreach (string line in lines)
        {
            if (line.StartsWith("#")) continue;

            string[] parts = line.Split('=');
            if (parts.Length == 2)
            {
                string buttonName = parts[0].Trim();
                string[] keyCodes = parts[1].Trim().Split(',');

                if (Enum.TryParse(buttonName, true, out Button button))
                {
                    var keyList = new List<(VirtualKeyCode, bool)>();

                    foreach (string keyCode in keyCodes)
                    {
                        string trimmedKey = keyCode.Trim();
                        bool holding = trimmedKey.StartsWith("H:");

                        if (holding)
                        {
                            trimmedKey = trimmedKey[2..].Trim();
                        }

                        if (Enum.TryParse(trimmedKey, out VirtualKeyCode virtualKey))
                        {
                            keyList.Add((virtualKey, holding));
                        }
                        else
                        {
                            if (trimmedKey != "")
                            {
                                Console.WriteLine($"Invalid key code: \"{trimmedKey}\"");
                            }
                        }
                    }

                    keyMappings[button] = keyList;
                    buttonStates[button] = (false, false);
                }
                else
                {
                    Console.WriteLine($"Invalid button: \"{buttonName}\"");
                }
            }
        }
    }

    /// <summary>
    /// Holds and adds custom configs and checks if they are contained in <paramref name="configFilePath"/>
    /// </summary>
    /// <param name="configFilePath"></param>
    static void LoadConfigs(string configFilePath)
    {
        if (File.Exists(configFilePath))
        {
            string[] configLines = File.ReadAllLines(configFilePath);

            foreach (string line in configLines)
            {
                string[] parts = line.Split(':');

                if (parts.Length == 2)
                {
                    string setting = parts[0].Trim();
                    string value = parts[1].Trim();

                    switch (setting)
                    {
                        case "Mouse Sensitivity X":
                            if (double.TryParse(value, out double parsedX))
                            {
                                sensitivityX = parsedX;
                            }
                            break;

                        case "Mouse Sensitivity Y":
                            if (double.TryParse(value, out double parsedY))
                            {
                                sensitivityY = parsedY;
                            }
                            break;

                        case "Steer Sensitivity X":
                            if (double.TryParse(value, out double parsedX2))
                            {
                                sensitivityX2 = parsedX2;
                            }
                            break;

                        case "Steer Sensitivity Y":
                            if (double.TryParse(value, out double parsedY2))
                            {
                                sensitivityY2 = parsedY2;
                            }
                            break;

                        case "Deadzone X":
                            if (double.TryParse(value, out double parsedDeadzoneX))
                            {
                                deadzoneX = parsedDeadzoneX;
                            }
                            break;

                        case "Deadzone Y":
                            if (double.TryParse(value, out double parsedDeadzoneY))
                            {
                                deadzoneY = parsedDeadzoneY;
                            }
                            break;

                        case "Mouse Scroll":
                            var keys = value.Split(',');
                            foreach (var key in keys)
                            {
                                if (Enum.TryParse(key.Trim(), true, out Button parsedButton))
                                {
                                    scrollKeys.Add(parsedButton);
                                }
                            }
                            break;

                        case "Joystick Keyboard":
                            var keys2 = value.Split(',');
                            foreach (var key in keys2)
                            {
                                if (Enum.TryParse(key.Trim(), true, out Button parsedButton))
                                {
                                    modeKeys.Add(parsedButton);
                                }
                            }
                            break;

                        case "Stop Program":
                            var keys3 = value.Split(',');
                            foreach (var key in keys3)
                            {
                                if (Enum.TryParse(key.Trim(), true, out Button parsedButton))
                                {
                                    stopKeys.Add(parsedButton);
                                }
                            }
                            break;

                        case "Steering Wheel":
                            var keys4 = value.Split(',');
                            foreach (var key in keys4)
                            {
                                if (Enum.TryParse(key.Trim(), true, out Button parsedButton))
                                {
                                    steerKeys.Add(parsedButton);
                                }
                            }
                            break;

                        /*
                    case "Use IR":
                        if (bool.TryParse(value, out bool parsedWheel))
                        {
                            useIR = parsedWheel;
                        }
                        break;
                        */

                        case "Show":
                            if (bool.TryParse(value, out bool parsedShow))
                            {
                                showConfigs = parsedShow;
                            }
                            break;

                        case "Debug":
                            if (bool.TryParse(value, out bool parsedDebug))
                            {
                                debug = parsedDebug;
                            }
                            break;

                        /*
                    case "Joystick Keyboard":
                        if (bool.TryParse(value, out bool parsedMousePressKey))
                        {
                            mousePressKey = parsedMousePressKey;
                        }
                        break;
                        */

                        default:
                            if (debug)
                                Console.WriteLine($"Unknown config: \"{setting}\"");
                            break;
                    }
                }
            }
        }
        else
        {
            Console.WriteLine($"Config file \"{configFilePath}\" not found.");
        }
    }

    /// <summary>
    /// Presses the key mapped to <paramref name="button"/>
    /// </summary>
    /// <param name="button"></param>
    static void PressKey(Button button)
    {
        if (keyMappings.ContainsKey(button))
        {
            foreach (var (key, holding) in keyMappings[button])
            {
                if (debug)
                {
                    Console.WriteLine($"\nPressed \"{key}\" on {button}");
                    wm.SetRumble(true);
                }

                if (!AllButtonsPressed(wm.WiimoteState, scrollKeys) && !AllButtonsPressed(wm.WiimoteState, modeKeys))
                {
                    if (key == VirtualKeyCode.LBUTTON)
                    {
                        inputSimulator.Mouse.LeftButtonDown();
                    }
                    else if (key == VirtualKeyCode.RBUTTON)
                    {
                        inputSimulator.Mouse.RightButtonDown();
                    }
                    else
                    {
                        inputSimulator.Keyboard.KeyDown(key);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Releases the key mapped to <paramref name="button"/>
    /// </summary>
    /// <param name="button"></param>
    static void ReleaseKey(Button button)
    {
        if (keyMappings.ContainsKey(button))
        {
            foreach (var (key, holding) in keyMappings[button])
            {
                if (debug)
                {
                    Console.WriteLine($"\nReleased \"{key}\" on {button}");
                    wm.SetRumble(false);
                }

                if (key == VirtualKeyCode.LBUTTON)
                {
                    inputSimulator.Mouse.LeftButtonUp();
                }
                else if (key == VirtualKeyCode.RBUTTON)
                {
                    inputSimulator.Mouse.RightButtonUp();
                }
                else
                {
                    inputSimulator.Keyboard.KeyUp(key);
                }
            }
        }
    }

    /// <summary>
    /// Stores every available ".wiimap" file path in a list and its name in another one
    /// </summary>
    public static void LoadFiles()
    {
        files.Clear();
        fileNames.Clear();

        string[] searchFiles = Directory.GetFiles(Directory.GetCurrentDirectory() + @"\Mappings\");

        foreach (string file in searchFiles)
        {
            if (file.EndsWith(".wiimap"))
            {
                files.Add(file);
                fileNames.Add(Path.GetFileName(file));

                /*
                Console.WriteLine(file);
                Console.WriteLine(Path.GetFileName(file));
                */
            }
        }

        if (files.Count == 0)
        {
            filePath = Directory.GetCurrentDirectory() + @"\Mappings\default.wiimap";
            File.WriteAllText(filePath, "");
        }
        else
        {
            filePath = files[0];
        }
    }

    public static void InitDefMap()
    {
        string path = Directory.GetCurrentDirectory() + @"\Mappings\default.wiimap";
        string text = "" +
            "# these are useful keys to map, you can find a list of all of them in the \"infos.txt\" file" +
            "\nUp = VK_W" +
            "\nDown = VK_S" +
            "\nLeft = VK_A" +
            "\nRight = VK_D" +
            "\nA = SPACE" +
            "\nC = LBUTTON" +
            "\nZ = RBUTTON";

        if (File.Exists(path) && (File.ReadAllText(path) == text || string.IsNullOrEmpty(File.ReadAllText(path))))
        {
            File.WriteAllText(path, text);
        }
    }
}