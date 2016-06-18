# Elite

This is a hacky workaround for mapping the Elite Controller paddles to keys. This should help when using tools like Auto Hotkey or simply avoiding the "quick save reach". To read about my experience creating the app, [visit my blog](http://shawnquereshi.com/2016/02/binding-the-elite-controller-paddles-to-the-keyboard/).

A helpful redditor put together [a more friendly set of instructions](https://www.reddit.com/r/xboxone/comments/468wv0/workaround_for_mapping_xbox_elite_controller/d45htf6) that I would recommend trying if these don't work. Thanks u/saviorbk!

### Prerequisites
* Xbox Elite Controller
* Xbox Accessories App
* Microsoft Build Tools (or VS2015)
* Nuget Command Line tool (or VS2015)
* Windows 10 SDK for certificate creation tools (or VS2015)

### Deployment Steps
1. Download and extract the package
2. Download the nuget command-line utility (https://docs.nuget.org/consume/installing-nuget)
3. Run nuget restore, targetting Elite.sln (e.g. "nuget.exe restore Elite.sln")
4. Run Install-ElitePaddles.ps1 from the directory where it is packaged, alongside Elite.sln

I suggest looking through Install-ElitePaddles.ps1 so you understand what it's doing. In summary, it will perform the following tasks:

1. Modify the dependencies of the Elite.csproj file to point to the directory of the XboxDevices app
2. Compile Elite.sln targeted to your current platform, which includes:
  * The ElitePaddles app
  * ElitePaddlesServiceHost which hosts a local service used by ElitePaddles to bypass the SendInput restrictions of UWP
3. ACL the URL http://+:8642/EliteService to the active user so ElitePaddlesServiceHost can listen on it
4. Generate a certificate to sign the appx package. The user will be prompted for passwords to create the certs, and then again to use them
6. Add the certificate to the root store and sign the appx package.
7. Deploy the ElitePaddles appx package.
5. Enable loopback on the ElitePaddles app

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
