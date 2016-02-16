# Elite

This is a hacky workaround for mapping the Elite Controller paddles to keys. This should help when using tools like Auto Hotkey or simply avoiding the "quick save reach". To read about my experience creating the app, [visit my blog](http://shawnquereshi.com/2016/02/binding-the-elite-controller-paddles-to-the-keyboard/).

### Prerequisites
* Xbox Elite Controller
* Xbox Accessories App
* Microsoft Build Tools (or VS2015)
* Windows 10 SDK for certificate creation tools (or VS2015)

### Deploying the Application

To deploy the app, run the packaged Install-ElitePaddles.ps1, which does the following:

1. Modify the dependencies of the project files to point to the directory of your XboxDevices app.
2. Compile Elite.sln. It is expected that the projects were not modified from how they were packeged with the solution.
3. Compile the ElitePaddlesServiceHost which is used to host a local service to bypass the SendInput restricts of UWP
4. Register the above url for the active user. Existing registration will be removed as it is assumed to be stale state.
5. Generate certificate to sign the appx package. The user will be prompted for passwords to create the certs, and then again to use them.
6. Add the certificate to the root store and sign the appx package.
7. Deploy the appx package.

### Running the Application

After deploying the app, do the following:
1. Start ElitePaddlesServiceHost.exe from the ElitePaddlesServiceHost\Out\ directory
2. Start ElitePaddles app

### Known or Potential Issues
* Multiple gamepads unsupported
* Large number of dependencies in deployment script
* Fiddling with deployment script may be required for some users as it was only lightly tested
* No autostart for the service host application
* Some keys cannot be mapped by the app because they select the buttons instead (e.g. Enter and gamepad "A")
* Poor error handling/diagnostic ability, for instance when service is not running
