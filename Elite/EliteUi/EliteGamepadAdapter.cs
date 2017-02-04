/// <author>
/// Shawn Quereshi
/// </author>
namespace EliteUi
{
    using System;
    using System.Linq;
    using GamepadManagement;
    using Windows.Foundation;

    /// <summary>
    /// Adapter with auto-reconnect for the EliteGamepad
    /// </summary>
    public class EliteGamepadAdapter
        : IDisposable
    {
        /// <summary>
        /// The lock for using the gamepad
        /// </summary>
        private readonly object gamepadLock = new object();

        /// <summary>
        /// The gamepad identifier
        /// </summary>
        private string gamepadId;

        /// <summary>
        /// The gamepad
        /// </summary>
        private EliteGamepad gamepad;

        /// <summary>
        /// True if the object has been disposed
        /// </summary>
        private bool disposed = false;

        EventHandler<EliteGamepad> addedHandler = null;
        EventHandler<EliteGamepad> removedHandler = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="EliteGamepadAdapter"/> class.
        /// </summary>
        /// <param name="gamepad">The gamepad.</param>
        private EliteGamepadAdapter(EliteGamepad gamepad)
        {
            this.gamepad = gamepad;
            this.gamepadId = gamepad.FriendlyName;
            this.IsReady = true;

            // Should make this disposable so they get removed, but it should never be a problem unless somebody is being naughty or ambitious :)
            this.addedHandler = (sender, data) =>
            {
                this.EnsureGamepadInitialized(data);
            };

            this.removedHandler = (sender, data) =>
            {
                this.RemoveGamepad(data);
            };

            EliteGamepad.EliteGamepadAdded += this.addedHandler;
            EliteGamepad.EliteGamepadRemoved += this.removedHandler;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the gamepad is ready for use
        /// </summary>
        public bool IsReady { get; set; } = false;

        /// <summary>
        /// Gets the current slot identifier.
        /// </summary>
        /// <value>
        /// The current slot identifier.
        /// </value>
        /// <exception cref="System.InvalidOperationException">The gamepad is uninitialized or disconnected.</exception>
        public SlotId CurrentSlotId
        {
            get
            {
                if (!this.IsReady)
                {
                    throw new InvalidOperationException("The gamepad is uninitialized or disconnected.");
                }

                return this.gamepad.CurrentSlotId;
            }
        }

        /// <summary>
        /// Tries to create a gamepad.
        /// </summary>
        /// <param name="gamepad">The gamepad.</param>
        /// <returns>True if a gamepad was created successfully; otherwise, false.</returns>
        public static bool TryCreate(out EliteGamepadAdapter gamepad)
        {
            EliteGamepad.CheckForDriverLoaded();
            var underlying = EliteGamepad.EliteGamepads.FirstOrDefault();
            if (underlying == null)
            {
                gamepad = null;
                return false;
            }

            var config = underlying.GetConfiguration(underlying.CurrentSlotId);
            gamepad = new EliteGamepadAdapter(underlying);
            return true;
        }

        /// <summary>
        /// Tries the create.
        /// </summary>
        /// <param name="gamepadFriendlyName">ID of the game pad trying to be created.</param>
        /// <param name="gamepad">The gamepad.</param>
        /// <returns>True if a gamepad was created successfully; otherwise, false.</returns>
        public static bool TryCreate(string gamepadFriendlyName, out EliteGamepadAdapter gamepad)
        {
            EliteGamepad.CheckForDriverLoaded();
            var underlying = EliteGamepad.EliteGamepads.FirstOrDefault(g => g.FriendlyName == gamepadFriendlyName);
            if (underlying == null)
            {
                gamepad = null;
                return false;
            }

            gamepad = new EliteGamepadAdapter(underlying);
            return true;
        }

        /// <summary>
        /// Gets the current unmapped reading.
        /// </summary>
        /// <returns>The reading.</returns>
        /// <exception cref="System.InvalidOperationException">The gamepad is uninitialized or disconnected.</exception>
        public GamepadReading GetCurrentUnmappedReading()
        {
            if (!this.IsReady)
            {
                throw new InvalidOperationException("The gamepad is uninitialized or disconnected.");
            }

            return this.gamepad.GetCurrentUnmappedReading();
        }

        /// <summary>
        /// Disconnects the gamepad.
        /// </summary>
        /// <param name="data">The data.</param>
        private void RemoveGamepad(EliteGamepad data)
        {
            if (this.gamepad == data)
            {
                this.IsReady = false;
            }
        }

        /// <summary>
        /// Connects the gamepad.
        /// </summary>
        /// <param name="gamepad">The gamepad.</param>
        private void EnsureGamepadInitialized(EliteGamepad gamepad)
        {
            lock (this.gamepadLock)
            {
                EliteGamepad.CheckForDriverLoaded();
                this.gamepad = gamepad;
                this.IsReady = true;
            }
        }

        public void Dispose()
        {
            if(!disposed)
            {
                EliteGamepad.EliteGamepadAdded -= this.addedHandler;
                EliteGamepad.EliteGamepadRemoved -= this.removedHandler;
            }
        }
    }
}
