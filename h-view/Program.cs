using System.Globalization;
using Hai.HView.Core;
using Hai.HView.Gui;
using Hai.HView.OSC;

var oscPort = HOsc.RandomOscPort();
var queryPort = HQuery.RandomQueryPort();

var oscClient = new HOsc(oscPort);
var oscQuery = new HQuery(oscPort, queryPort);
oscQuery.OnVrcOscPortFound += vrcOscPort => oscClient.SetReceiverOscPort(vrcOscPort);

var messageBox = new HMessageBox();
var routine = new HVRoutine(oscClient, oscQuery, messageBox);

oscClient.Start();
oscQuery.Start();
routine.Start();

void WhenWindowClosed()
{
    routine.Finish();
    oscQuery.Finish();
    oscClient.Finish();
}

var uiThread = new Thread(() => new HVWindow(routine, WhenWindowClosed).Run())
{
    CurrentCulture = CultureInfo.InvariantCulture, // We don't want locale-specific numbers
    CurrentUICulture = CultureInfo.InvariantCulture
};
uiThread.Start();

routine.MainLoop(); // This call does not return until routine.Finish() is called.
