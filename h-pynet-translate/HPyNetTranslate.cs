using Python.Runtime;

namespace HView.PythonNet.Translate;

public class HPyNetTranslate
{
    private IntPtr _threadState;
    private readonly object _pythonLock = new();

    public void Start()
    {
        Runtime.PythonDLL = "python312.dll";
        PythonEngine.Initialize();
        _threadState = PythonEngine.BeginAllowThreads();
    }

    public async Task<string> Translate(string text, string from, string to)
    {
        return await Task.Run(() => InnerTranslate(text, from, to));
    }

    private string InnerTranslate(string text, string from, string to)
    {
        lock (_pythonLock)
        {
            try
            {
                string result;
                using (Py.GIL())
                {
                    dynamic translate = Py.Import("argostranslate.translate");
                    result = (string)translate.translate(text, from, to);
                }
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }

    public void Teardown()
    {
        PythonEngine.EndAllowThreads(_threadState);
        PythonEngine.Shutdown();
    }
}