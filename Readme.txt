/*
 * Reactive Extensions (Rx) sample of building IQbservable<T> providers.
 * For more information about Rx, see http://msdn.microsoft.com/en-us/devlabs/ee794896.aspx
 */

This sample shows how to implement IQbservable<T> and write a basic observable query provider
for WMI event querying. LINQ-based queries are translated into WQL queries that are observed
using the ManagementWatcher class in the BCL.


To run the sample:

1. Open a Visual Studio 2010 instance, running as an Administrator. This is required to make
   sure WMI events can be watched.
2. Check references to System.CoreEx and System.Reactive in the class library project called
   "Microsoft.DevLabs.Reactive.Bridge.Instrumentation". If necessary, update those to refer
   to the latest version of your Rx installation. To download Rx, see the link above.
3. Build the entire solution.
4. From an elevated Visual Studio 2010 Command Prompt window, run installutil.exe -i on the
   compiled DemoEventProvider.dll file in the bin\Debug folder of DemoEventProvider. This step
   is only needed if you want to run the custom WMI event provider sample.
5. Run the Demo project.


Expected output looks as follows (notice you need to press ENTER after each demo to continue,
see code in Demo's Main method for more context):

LINQ to WQL - Query pattern
---------------------------

SELECT ProcessName, ProcessID FROM Win32_ProcessStartTrace WHERE ProcessName = "
notepad.exe"
{ Name = notepad.exe, ID = 6208 }@9/14/2010 2:56:54 PM -07:00
{ Name = notepad.exe, ID = 5932 }@9/14/2010 2:56:56 PM -07:00
{ Name = notepad.exe, ID = 1308 }@9/14/2010 2:56:58 PM -07:00
{ Name = notepad.exe, ID = 8148 }@9/14/2010 2:57:00 PM -07:00
{ Name = notepad.exe, ID = 5520 }@9/14/2010 2:57:02 PM -07:00


LINQ to WQL - Qbservable
------------------------

SELECT ProcessName, ProcessID FROM Win32_ProcessStartTrace WHERE ProcessName = "
notepad.exe"
{ Name = notepad.exe, ID = 2156 }@9/14/2010 2:57:14 PM -07:00
{ Name = notepad.exe, ID = 8100 }@9/14/2010 2:57:16 PM -07:00
{ Name = notepad.exe, ID = 8112 }@9/14/2010 2:57:18 PM -07:00
{ Name = notepad.exe, ID = 5948 }@9/14/2010 2:57:20 PM -07:00
{ Name = notepad.exe, ID = 4140 }@9/14/2010 2:57:22 PM -07:00


LINQ to WQL - Custom Events
---------------------------

0
1
2
3
4
5
6
7
8
9
Completed