using GamepadManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace EliteUi
{
    public class SettingsManager
    {
        private static SettingsManager instance;
        public static SettingsManager Instance
        {
            get
            {
                return instance ?? (instance = new SettingsManager());
            }
        }
        
        private ApplicationDataContainer settings;

        private SettingsManager()
        {
            settings = ApplicationData.Current.LocalSettings;
        }

        public void SaveButtonAssignment(GamepadButtons button, VirtualKey assignment)
        {
            settings.Values[button.ToString()] = assignment.ToString();
        }

        public void RemoveButtonAssignment(GamepadButtons button)
        {
            settings.Values.Remove(button.ToString());
        }

        public bool TryLoadButtonAssignment(GamepadButtons button, out VirtualKey assignment)
        {
            assignment = VirtualKey.None;
            var value = settings.Values[button.ToString()] as string;
            if(value == null)
            {
                return false;
            }

            if (Enum.TryParse<VirtualKey>(value, out assignment))
            {
                return true;
            }

            return false;
        }
    }
}
