using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO;
namespace AIka
{
    class Program
    {
        #region user32 chamadas

        [DllImport("user32.dll")]
        static extern bool GetAsyncKeyState(Keys vKey);

        [DllImport("user32.dll")]
        static extern int SetForegroundWindow(IntPtr point);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, int Msg, System.Windows.Forms.Keys wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowRect(IntPtr hwnd, out Rectangle rect);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOZORDER = 0x0004;
        #endregion

        #region  Variaveis

        //Configuraçao do pixels..
        private static Color CorInimigo = Color.FromArgb(0xDD, 0xDD, 0xBB);
        private static Color CorSaudeBaixa = Color.FromArgb(0xA4, 0x00, 0x03);
        private static Rectangle InimigoGUIArea = new Rectangle(214, 0, 586, 106);
        private static Rectangle PerfilGUIArea = new Rectangle(62, 49, 137, 106);

        private static bool Debug = false; //
        private static bool AtacandoInimigo = false; //Se tem algum inimigo no focu
        private static int QuantidadeMob = 0; //Quantodade de mobs eliminados
        private static int CurasUsadas = 0;
        private static int SkillIndex = 0; //Index da Skill
        private static bool Rodando = false; //Variavel para ver se está o não rodando.
        private static int Tentativas = 0;

        private static ConfigKeys Configuracao; //Estrutura para configurar teclas de atalho.
        private static Thread tRotina; //Thread inicial do Bot
        private static Thread tTecla; //Thread para capturar teclas apertadas.
        private static Process ProcJogo; //Processo do jogo.
        private static DateTime HoraInicio; //Horario de inicio do bot.
        #endregion
        class ConfigKeys
        {
            public Keys Skill1 { get; set; }
            public Keys Skill2 { get; set; }
            public Keys Skill3 { get; set; }
            public Keys Cura1 { get; set; }
            public Keys Cura2 { get; set; }
            public Keys IniciarBot { get; set; }
            public Keys PararBot { get; set; }
            public static ConfigKeys Padrao()
            {
                return new ConfigKeys { Skill1 = Keys.NumPad1, Skill2 = Keys.NumPad2, Skill3 = Keys.NumPad3, IniciarBot = Keys.F1, PararBot = Keys.F2, Cura1 = Keys.NumPad5, Cura2 = Keys.NumPad6 };
            }
        }

        #region Rotinas Principais Main, thRotina, KeyStatus
        static void Main(string[] args)
        {
            GerarListaKeys();
            Configuracao = null;
            print_msg("Lendo configuracoes...");
            if (File.Exists("Config.json"))
                Configuracao = JsonConvert.DeserializeObject<ConfigKeys>(File.ReadAllText("Config.json"));
            else
                Configuracao = ConfigKeys.Padrao();

            if (Configuracao == null)
                Configuracao = ConfigKeys.Padrao();

            File.WriteAllText("Config.json", JsonConvert.SerializeObject(Configuracao));
            print_msg("Iniciando buscando processos do ");
            print_mark("AIKA");


            if (!BuscarProcesso(out ProcJogo))
            {
                print_error("Janela do jogo nao foi encontrada.");
                Console.ReadLine();
                return;
            }

            print_info(string.Format("Janela {0} encontrada [0x{1}] ModuloBase: [0x{2}] ModuloTamanho:[0x{3}]", ProcJogo.MainWindowTitle, ProcJogo.MainWindowHandle.ToInt32().ToString("X"), ProcJogo.MainModule.BaseAddress.ToString("X"), ProcJogo.MainModule.ModuleMemorySize.ToString("X")));

            print_info(string.Format("Pressione {0} para iniciar ou {1} para parar.", Configuracao.IniciarBot.ToString(), Configuracao.IniciarBot.ToString()));
             Iniciar();
            
            tTecla = new Thread(KeyStatus);
            tTecla.Start();

            //Rotina para reparar uns bugs..;
            new Thread(RotinaSeguranca).Start();
            while (true) { }

        }
        static void KeyStatus()
        {
            while (true)
            {
                Console.Title = string.Format("Aika BOT 1.0| [RODANDO: {0}] [Mobs mortos: {1}] [Horario de Inicio: {2}]", Rodando, QuantidadeMob, HoraInicio.ToString());
                if (GetAsyncKeyState(Configuracao.IniciarBot))
                {
                    if (!Rodando)
                    {
                        if (tRotina.IsAlive)
                            tRotina.Abort();
                        Iniciar();
                    }

                }
               
                if (GetAsyncKeyState(Configuracao.PararBot))
                {
                    if (Rodando)
                    {
                        Rodando = false;
                        if (tRotina.IsAlive)
                            tRotina.Abort();
                        print_info("Bot abortado...\n");
                        print_mark("\t\tRelatorio");
                        print_msg(string.Format("Tempo total: {0}", DateTime.Now.Subtract(HoraInicio).ToString()));
                        print_msg(string.Format("Mobs enfrentados: {0}", QuantidadeMob));
                        print_msg(string.Format("Curas usadas: {0}", CurasUsadas));
                        print_msg(string.Format("Parou na skill: {0}", SkillIndex + 1));
                    }
                }
                Thread.Sleep(300);
            }
        }
        static void RotinaSeguranca()
        {
            while (true)
            {
                if (Rodando)
                {
                    Tentativas++;
                    if (!AtacandoInimigo)
                    {
                        Tentativas = 0;
                    }
                    Thread.Sleep(1000);
                }
            }
        }
        static void ThRotina()
        {
            while (Rodando)
            {
                Thread.Sleep(1000);
                Point Local;

                if (ProcJogo.HasExited)
                {
                    break;
                }

                SetForegroundWindow(ProcJogo.MainWindowHandle);
                Rectangle Saida = Rectangle.Empty;
                GetWindowRect(ProcJogo.MainWindowHandle, out Saida);
                if (Saida != Rectangle.Empty)
                {
                    SetWindowPos(ProcJogo.MainWindowHandle, IntPtr.Zero, 0, 0, 0,0, SWP_NOSIZE | SWP_NOZORDER);
                    SetWindowPos(Process.GetCurrentProcess().MainWindowHandle, IntPtr.Zero, Saida.Width, 0, 600, 500, SWP_NOZORDER);
                    
                }

                if (VerificarEmArea(PerfilGUIArea, CorSaudeBaixa, out Local))
                {
                    SendKey(Keys.S);
                    CurasUsadas++;
                    print_info("Saude baixa usando Cura e Recuando.");
                    SendKey(Configuracao.Cura1, 1500);
                    Thread.Sleep(100);
                    continue;
                }
                if (!VerificarEmArea(InimigoGUIArea, CorInimigo, out Local))
                {
                    if (AtacandoInimigo)
                    {
                        print_msg("Voce adquiriu ");
                        print_mark("+1 kill.");
                        QuantidadeMob++;
                        AtacandoInimigo = false;
                    }
                }
                else
                    AtacandoInimigo = true;

                if (Tentativas >= 40)
                {
                    print_error("Inimigo demorou mais de 40seg para matar, possivel bug, procurando proximo inimigo.");
                    AtacandoInimigo = false;
                    Tentativas = 0;
                    continue;
                }
                if (!AtacandoInimigo)
                {
                    print_info("Buscando inimigos...");
                    for (int il = 0; il < 15; il++)
                        SendKey(Keys.D);
                    SendKey(Keys.Tab);
                }
                else
                {

                    switch (SkillIndex)
                    {
                        case 0:
                            SendKey(Configuracao.Skill1);
                            break;
                        case 1:
                            SendKey(Configuracao.Skill2);
                            break;
                        case 2:
                            SendKey(Configuracao.Skill3);
                            break;
                        default:
                            SkillIndex = 0;
                            break;
                    }
                    SkillIndex++;
                }

            }
            print_info("Bot pausado ou processo encerrado.");
        }
        static void Iniciar()
        {
            AtacandoInimigo = false;
            HoraInicio = DateTime.Now;
            QuantidadeMob = 0;
            Rodando = true;
            CurasUsadas = 0;
            tRotina = new Thread(ThRotina);
            tRotina.Start();
            print_info("Bot iniciado...");

        }
        #endregion
        #region Funçoes para o Aika

        static void GerarListaKeys()
        {
            string TextoFinal = "Lista de teclas e suas ID's para configuração do AikaBot." + Environment.NewLine;
            foreach (Keys foo in Enum.GetValues(typeof(Keys)))
            {
                TextoFinal += string.Format("[{0},{1}]]{2}", (int)foo, foo.ToString(), Environment.NewLine);
            }
            File.WriteAllText("IDS KEYS.txt", TextoFinal);
        }
        public static void SendKey(Keys key, int delay = 0)
        {
            PostMessage(ProcJogo.MainWindowHandle, 0x100, key, 0);
            Thread.Sleep(delay);
            PostMessage(ProcJogo.MainWindowHandle, 0x101, key, 0);
        }
        static bool BuscarProcesso(out Process Saida)
        {
            Process Retorno = Process.GetProcessesByName("AIKAEN").FirstOrDefault();
            if (Retorno == null)
                Retorno = Process.GetProcessesByName("AIKABR").FirstOrDefault();

            Saida = Retorno;
            if (Retorno == null)
                return false;
            else
                return true;
        }
        #endregion
        #region Funcoes para recog
        private static Bitmap Screenshot()
        {
            Bitmap bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            Graphics g = Graphics.FromImage(bmpScreenshot);
            g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);
            return bmpScreenshot;
        }
        static bool VerificarEmArea(Rectangle Area, Color cor, out Point Local)
        {
            Bitmap bit = Screenshot();
            for (int x = Area.X; x < (Area.Width + Area.X); x++)
            {
                for (int y = Area.Y; y < Area.Height + Area.Y; y++)
                {
                    Color Pixel = bit.GetPixel(x, y);
                    if ((Pixel.R == cor.R) && (Pixel.B == cor.B) && (Pixel.G == cor.G))
                    {
                        if (Debug)
                            Console.WriteLine("{0},{1}", x, y);
                        Local = new Point(x, y);
                        return true;
                    }
                }
            }
            Local = new Point(0, 0);
            return false;
        }
        static bool VerificarPixel(Color cor, Point Local)
        {
            Bitmap btTela = Screenshot();
            Color pixel = btTela.GetPixel(Local.X, Local.Y);
            return (pixel.R == cor.R && pixel.G == cor.G && pixel.B == cor.B);
        }
        #endregion
        #region Prints Utilidade para colorir
        static void print_mark(string msg)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.Write(msg);
        }
        static void print_msg(string msg)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.Write("[*] {0}", msg);
        }
        static void print_error(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.Write("[*] Error: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(msg);

        }
        static void print_info(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine();
            Console.Write("[*] Info: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(msg);
        }
        #endregion 
    }
}
