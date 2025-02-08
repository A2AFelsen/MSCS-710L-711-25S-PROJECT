A github repository for Michael Huffer, Michael Laffan, and Frank Seelmann's MSCS-710L-711-25S-PROJECT.

Discord Server Invite Link:
https://discord.gg/Kwb6VZGQYP


Steps to run OpenHardwareMonitor:

1. Download latest version of .Net Framework, preferably 4.8.1.
https://dotnet.microsoft.com/en-us/download/dotnet-framework

2. Open Visual Studio and click Create New Project.

3. In the languages tab, select C#. Then click "Install more tools and Features" and install ".NET desktop development".

4. Click "Open a project or solution" and navigate to the solution file in the OpenHardware Monitor folder.

5. After the solution finshes loading, right click References in the Solution Explorer and click Browse. Navigate to the OpenHardwareMonitorLib.dll file in the OpenHardwareMonitor folder and select it, then click OK.

6. Build the project and run it. The interval for collecting metrics is set for 30 seconds, but can be changed by modifying the timer variable, which is in milliseconds.