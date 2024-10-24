using System.Text;
using Python.Runtime;

Runtime.PythonDLL = "python312.dll";

PythonEngine.Initialize();
var result = "";
using (Py.GIL())
{
    dynamic package = Py.Import("argostranslate.package");
    dynamic translate = Py.Import("argostranslate.translate");

    result = translate.translate("Good morning", "en", "ja");
}
File.WriteAllText("output.txt", result, Encoding.UTF8);
PythonEngine.Shutdown();
