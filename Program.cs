using System;
using System.Linq;
using System.Windows.Forms;

namespace PT2
{
    static class Program
    {
        [STAThread]
        static void Main(string[] aargs)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (aargs.Contains("/sillent") || aargs.Contains("-sillent") || aargs.Contains("sillent"))
            {
                //bool IsAuthenticated = true;
                string[] argsToPass = (aargs != null && aargs.Length > 0) ? aargs : new string[0];
                Application.Run(new MainForm(argsToPass));
            }
            else
            {
                LoginForm loginForm = new LoginForm();
                Application.Run(loginForm);

                if (loginForm.IsAuthenticated)
                {
                    string[] argsToPass = (aargs != null && aargs.Length > 0) ? aargs : new string[0];
                    Application.Run(new MainForm(argsToPass));
                }
            }
        }
    }
}