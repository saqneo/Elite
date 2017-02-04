/// <author>
/// Shawn Quereshi
/// </author>
namespace EliteUi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using GamepadManagement;
    using Windows.Foundation;
    using Windows.System;
    using Windows.UI;
    using Windows.UI.Core;
    using Windows.UI.ViewManagement;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media;
    using Windows.Gaming;
    using System.Reflection;

    /// <summary>
    /// The non-generated part of the main application.
    /// </summary>
    /// <seealso cref="Windows.UI.Xaml.Controls.Page" />
    /// <seealso cref="Windows.UI.Xaml.Markup.IComponentConnector" />
    /// <seealso cref="Windows.UI.Xaml.Markup.IComponentConnector2" />
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// The service URI
        /// </summary>
        private const string EliteServiceUri = "http://localhost:8642/EliteService";

        /// <summary>
        /// The poll rate for the gamepad
        /// </summary>
        private const int PollRateMs = 8;

        /// <summary>
        /// The timer synchronization lock
        /// </summary>
        private object timerLock = new object();

        /// <summary>
        /// The timer
        /// </summary>
        private Timer timer;

        /// <summary>
        /// The last timestamp of the gamepad
        /// </summary>
        private ulong lastTimestamp = 0;

        /// <summary>
        /// The gamepad
        /// </summary>
        private EliteGamepadAdapter gamepad = null;

        #region app state properties

        /// <summary>
        /// The lock for click processing
        /// </summary>
        private object clickLock = new object();

        /// <summary>
        /// The buttons which currently have mappings
        /// </summary>
        private IDictionary<GamepadButtons, VirtualKey> assignedButtons = new Dictionary<GamepadButtons, VirtualKey>();

        /// <summary>
        /// The currently pressed
        /// </summary>
        private HashSet<GamepadButtons> buttonsDown = new HashSet<GamepadButtons>();

        /// <summary>
        /// Whether a key is currently being assigned
        /// </summary>
        private bool assigning = false;

        /// <summary>
        /// The gamepad button being assigned
        /// </summary>
        private GamepadButtons assignedGamepadButton = GamepadButtons.None;

        /// <summary>
        /// The last UI button clicked ("being assigned")
        /// </summary>
        private Button assignedButton = null;

        /// <summary>
        /// Whether clearing is being deferred (used for assigning spacebar)
        /// </summary>
        private bool deferringClear = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the application.
        /// </summary>
        private void Initialize()
        {
            this.InitializeWindow();
            this.InitializeButtons();
            this.InitializeTimer();
            this.InitializeEvents();
        }

        private void InitializeButtons()
        {
            var initializeButtons = new[]{ GamepadButtons.Aux1, GamepadButtons.Aux2, GamepadButtons.Aux3, GamepadButtons.Aux4 };
            foreach(var button in initializeButtons)
            {
                VirtualKey key;
                if (SettingsManager.Instance.TryLoadButtonAssignment(button, out key))
                {
                    AssignButton(button, key);
                }
            }
        }

        /// <summary>
        /// Initializes the application window.
        /// </summary>
        private void InitializeWindow()
        {
            ApplicationView.PreferredLaunchViewSize = new Size { Height = 400, Width = 320 };
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            ApplicationView.GetForCurrentView().TryResizeView(new Size { Height = 400, Width = 320 });
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size { Height = 400, Width = 320 });
        }

        /// <summary>
        /// Initializes the timer for polling.
        /// </summary>
        private void InitializeTimer()
        {
            this.timer = new Timer(new TimerCallback(this.TimerCallback), this, 0, PollRateMs);
        }

        /// <summary>
        /// Initializes any events being used that aren't implicitly initialized.
        /// </summary>
        private void InitializeEvents()
        {
            // If we want to intercept keys before they hit buttons so we can use Spacebar
            CoreWindow.GetForCurrentThread().KeyDown += (o, e) =>
            {
                if (e.VirtualKey == VirtualKey.Space)
                {
                    this.OnKeyDown(e.VirtualKey, false);
                }
            };
        }

        #endregion

        #region Background controller polling

        /// <summary>
        /// The function to do processing for the timer
        /// </summary>
        /// <param name="state">The object holding the state to be used by the timer..</param>
        private void TimerCallback(object state)
        {
            if (!Monitor.TryEnter(this.timerLock))
            {
                return;
            }

            try
            {
                if ((this.gamepad == null && !EliteGamepadAdapter.TryCreate(out this.gamepad)) || !this.gamepad.IsReady)
                {
                    // Workaround for callback not working when controller is added.
                    if(!this.gamepad.IsReady)
                    {
                        this.gamepad = null;
                    }

                    return;
                }

                var reading = this.gamepad.GetCurrentUnmappedReading();
                if (this.ShouldSkipGamepadProcessing(reading))
                {
                    return;
                }

                this.WriteGamepadReadings(reading);
                this.ProcessGamepadReadings(reading);
            }
            catch (Exception)
            {
                // Do nothing, gamepad could be uninitialized
            }
            finally
            {
                Monitor.Exit(this.timerLock);
            }
        }

        /// <summary>
        /// Whether gamepad processing should be skipped due to the current and previous reading being the same.
        /// </summary>
        /// <param name="reading">The reading.</param>
        /// <returns>True if the reading is new and should be processed; otherwise, false.</returns>
        private bool ShouldSkipGamepadProcessing(GamepadReading reading)
        {
            var newTimestamp = reading.Timestamp;
            if (this.lastTimestamp == reading.Timestamp)
            {
                return true;
            }

            this.lastTimestamp = newTimestamp;
            return false;
        }

        /// <summary>
        /// Prints the gamepad readings to the UI
        /// </summary>
        /// <param name="reading">The reading.</param>
        private void WriteGamepadReadings(GamepadReading reading)
        {
            var propertyStringBuilder = new StringBuilder();
            var valueStringBuilder = new StringBuilder();

            valueStringBuilder.AppendLine(this.gamepad.CurrentSlotId.ToString());
            propertyStringBuilder.AppendLine(string.Format("Slot:"));

            valueStringBuilder.AppendLine(reading.Buttons.ToString());
            propertyStringBuilder.AppendLine("Buttons:");

            valueStringBuilder.AppendLine(reading.LeftTrigger.ToString());
            propertyStringBuilder.AppendLine("LeftTrigger:");

            valueStringBuilder.AppendLine(reading.RightTrigger.ToString());
            propertyStringBuilder.AppendLine("RightTrigger:");

            valueStringBuilder.AppendLine(reading.LeftThumbstickX.ToString());
            propertyStringBuilder.AppendLine("LeftThumbstickX:");

            valueStringBuilder.AppendLine(reading.LeftThumbstickY.ToString());
            propertyStringBuilder.AppendLine("LeftThumbstickY:");

            valueStringBuilder.AppendLine(reading.RightThumbstickX.ToString());
            propertyStringBuilder.AppendLine("RightThumbstickX:");

            valueStringBuilder.AppendLine(reading.RightThumbstickY.ToString());
            propertyStringBuilder.AppendLine("RightThumbstickY:");

            var task = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { measurementId.Text = propertyStringBuilder.ToString(); }).AsTask();
            var task2 = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { measurementValue.Text = valueStringBuilder.ToString(); }).AsTask();
            Task.WaitAll(task, task2);
        }

        /// <summary>
        /// Processes the gamepad readings for sending the assigned key.
        /// </summary>
        /// <param name="reading">The reading.</param>
        private void ProcessGamepadReadings(GamepadReading reading)
        {
            var values = (GamepadButtons[])Enum.GetValues(typeof(GamepadButtons));

            var tasks = new HashSet<Task>();
            foreach (var value in values.Where(v => (reading.Buttons & v) != 0))
            {
                VirtualKey key;
                if (this.assignedButtons.TryGetValue(value, out key) && !this.buttonsDown.Contains(value))
                {
                    this.buttonsDown.Add(value);
                    InjectionEngine.Instance.SendKeyDown(key);
                }

                switch (reading.Buttons & value)
                {
                    case GamepadButtons.Aux1:
                        tasks.Add(Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { aux1_identifier.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0x7C, 0x10)); }).AsTask());
                        break;
                    case GamepadButtons.Aux2:
                        tasks.Add(Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { aux2_identifier.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0x7C, 0x10)); }).AsTask());
                        break;
                    case GamepadButtons.Aux3:
                        tasks.Add(Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { aux3_identifier.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0x7C, 0x10)); }).AsTask());
                        break;
                    case GamepadButtons.Aux4:
                        tasks.Add(Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { aux4_identifier.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0x7C, 0x10)); }).AsTask());
                        break;
                    default:
                        break;
                }
            }

            foreach (var value in values.Where(v => (reading.Buttons & v) == 0))
            {
                if (this.buttonsDown.Contains(value))
                {
                    VirtualKey key;
                    if (this.assignedButtons.TryGetValue(value, out key))
                    {
                        this.buttonsDown.Add(value);
                        InjectionEngine.Instance.SendKeyUp(key);
                    }

                    this.buttonsDown.Remove(value);
                }

                if (this.buttonsDown.Contains(value))
                {
                    this.buttonsDown.Remove(value);
                }

                switch (value)
                {
                    case GamepadButtons.Aux1:
                        tasks.Add(Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { aux1_identifier.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x3A, 0x3A)); }).AsTask());
                        break;
                    case GamepadButtons.Aux2:
                        tasks.Add(Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { aux2_identifier.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x3A, 0x3A)); }).AsTask());
                        break;
                    case GamepadButtons.Aux3:
                        tasks.Add(Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { aux3_identifier.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x3A, 0x3A)); }).AsTask());
                        break;
                    case GamepadButtons.Aux4:
                        tasks.Add(Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { aux4_identifier.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x3A, 0x3A)); }).AsTask());
                        break;
                    default:
                        break;
                }
            }

            // Wait for UI to update
            Task.WaitAll(tasks.ToArray<Task>());
        }

        #endregion

        #region UI events and handling

        /// <summary>
        /// Handles the Click event of the aux1_button control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        public void aux1_button_Click(object sender, RoutedEventArgs e)
        {
            lock (this.clickLock)
            {
                this.ProcessButtonClick(e.OriginalSource as Button, GamepadButtons.Aux1);
            }
        }

        /// <summary>
        /// Handles the Click event of the aux2_button control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        public void aux2_button_Click(object sender, RoutedEventArgs e)
        {
            lock (this.clickLock)
            {
                this.ProcessButtonClick(e.OriginalSource as Button, GamepadButtons.Aux2);
            }
        }

        /// <summary>
        /// Handles the Click event of the aux3_button control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        public void aux3_button_Click(object sender, RoutedEventArgs e)
        {
            lock (this.clickLock)
            {
                this.ProcessButtonClick(e.OriginalSource as Button, GamepadButtons.Aux3);
            }
        }

        /// <summary>
        /// Handles the Click event of the aux4_button control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        public void aux4_button_Click(object sender, RoutedEventArgs e)
        {
            lock (this.clickLock)
            {
                this.ProcessButtonClick(e.OriginalSource as Button, GamepadButtons.Aux4);
            }
        }

        /// <summary>
        /// Raised from the the <see cref="E:KeyDown" /> event.
        /// </summary>
        /// <param name="e">The <see cref="KeyRoutedEventArgs"/> instance containing the event data.</param>
        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            base.OnKeyDown(e);
            this.OnKeyDown(e.Key);
        }

        /// <summary>
        /// Called when [key down].
        /// </summary>
        /// <param name="key">The key.</param>
        private void OnKeyDown(VirtualKey key)
        {
            this.OnKeyDown(key, true);
        }

        /// <summary>
        /// Called when [key down].
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="reset">if set to <c>true</c> [reset].</param>
        private void OnKeyDown(VirtualKey key, bool reset)
        {
            if (!this.assigning)
            {
                return;
            }

            lock (this.clickLock)
            {
                if (this.assigning)
                {
                    AssignButton(this.assignedGamepadButton, key);

                    // Workaround that certain characters get captured twice 
                    this.deferringClear = !reset;
                    if (reset)
                    {
                        this.EnableButtons();
                        this.ClearAssignmentVariables();
                    }
                }
            }
        }

        private void UnassignButton(GamepadButtons button)
        {
            switch (button)
            {
                case GamepadButtons.Aux1:
                    aux1_button.Content = "Unassigned";
                    break;
                case GamepadButtons.Aux2:
                    aux2_button.Content = "Unassigned";
                    break;
                case GamepadButtons.Aux3:
                    aux3_button.Content = "Unassigned";
                    break;
                case GamepadButtons.Aux4:
                    aux4_button.Content = "Unassigned";
                    break;
                default:
                    return;
            }

            this.assignedButtons.Remove(button);
            SettingsManager.Instance.RemoveButtonAssignment(button);
        }

        private void AssignButton(GamepadButtons button, VirtualKey key)
        {
            switch(button)
            {
                case GamepadButtons.Aux1:
                    aux1_button.Content = key.ToString();
                    break;
                case GamepadButtons.Aux2:
                    aux2_button.Content = key.ToString();
                    break;
                case GamepadButtons.Aux3:
                    aux3_button.Content = key.ToString();
                    break;
                case GamepadButtons.Aux4:
                    aux4_button.Content = key.ToString();
                    break;
                default:
                    return;
            }

            this.assignedButtons[button] = key;
            SettingsManager.Instance.SaveButtonAssignment(this.assignedGamepadButton, key);
        }

        /// <summary>
        /// Processes a button click.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <param name="gamepadButton">The gamepad button associated with the UI button.</param>
        private void ProcessButtonClick(Button button, GamepadButtons gamepadButton)
        {
            if (!this.assigning)
            {
                this.DisableButtonsExcept(button);
                this.SetAssignmentVariables(button, gamepadButton);
            }
            else
            {
                if (!this.deferringClear)
                {
                    UnassignButton(gamepadButton);
                }

                this.ClearAssignmentVariables();
                this.EnableButtons();
                this.deferringClear = false;
            }
        }

        /// <summary>
        /// Clears assignment variables after an assignment or canceled assignment.
        /// </summary>
        private void ClearAssignmentVariables()
        {
            this.assigning = false;
            this.assignedGamepadButton = GamepadButtons.None;
            this.assignedButton = null;
        }

        /// <summary>
        /// Sets the assignment variables to begin button assignment.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <param name="gamepadButton">The gamepad button.</param>
        private void SetAssignmentVariables(Button button, GamepadButtons gamepadButton)
        {
            this.assigning = true;
            this.assignedGamepadButton = gamepadButton;
            this.assignedButton = button;
        }

        /// <summary>
        /// Enables the UI buttons.
        /// </summary>
        private void EnableButtons()
        {
            var toEnable = new HashSet<Button>() { aux1_button, aux2_button, aux3_button, aux4_button };
            foreach (var enabled in toEnable)
            {
                enabled.IsEnabled = true;
            }
        }

        /// <summary>
        /// Disables the UI buttons except the specified button.
        /// </summary>
        /// <param name="button">The button.</param>
        private void DisableButtonsExcept(Button button)
        {
            var toDisable = new HashSet<Button>() { aux1_button, aux2_button, aux3_button, aux4_button };
            toDisable.Remove(button);

            foreach (var enabled in toDisable)
            {
                enabled.IsEnabled = false;
            }
        }

        #endregion
    }
}
