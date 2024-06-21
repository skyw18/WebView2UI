using Microsoft.Web.WebView2.Core;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WebView2UI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //一些注释，另有一些在MainWindow.xaml文件
        //微软官方教学文档见： https://learn.microsoft.com/zh-cn/microsoft-edge/webview2/get-started/wpf
        //示例代码见   https://github.com/MicrosoftEdge/WebView2Samples     https://learn.microsoft.com/zh-cn/microsoft-edge/webview2/samples/webview2wpfbrowser
        //   安装配置webview2方法
        //      a. 从nuget之中获取Microsoft.Web.WebView2
        //      b. xaml之中插入 xmlns:wv2="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf"
        //1  本机代码发送信息到web    使用webView.CoreWebView2.PostWebMessageAsString 和 webView.CoreWebView2.PostWebMessageAsJSON
        //   然后在html页面之中使用window.chrome.webview.addEventListener 处理字符串，或者json
        //2  Web内容发送信息到本机代码 在html页面之中使用js window.chrome.webview.postMessage(信息);  
        //   然后本机端 webView.WebMessageReceived += WebView_WebMessageReceived;
        //   在void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)  之中处理信息
        //3  本机让web端运行代码   使用await webView.CoreWebView2.ExecuteScriptAsync(text); 直接执行，text内为脚本内容
        //   也可以使用js脚本文件string text = System.IO.File.ReadAllText(@"C:\PATH_TO_YOUR_FILE\script.js");

        private const float UI_SCALE = 1.75f; //Windows设置之中的UI缩放比例 100%为1.0f 50%为0.5f 以此类推
        private bool isDrag = false; //是否处于拖动窗口状态
        private int dragStartX = 0;
        private int dragStartY = 0;
        private int winStartLeft = 0;
        private int winStartTop = 0;

        [System.Runtime.InteropServices.DllImport("user32.dll")] //导入user32.dll函数库
        public static extern bool GetCursorPos(out System.Drawing.Point lpPoint);//获取鼠标坐标


        private System.Drawing.Point GetMousePose()
        {
            System.Drawing.Point mp = new System.Drawing.Point();
            GetCursorPos(out mp);
            //Console.WriteLine($"====鼠标坐标 ==== {mp.X},{mp.Y}");
            return mp;
        }
        /// <summary>
        /// 后台线程，每10ms获取一次当前鼠标坐标，如果处于拖动状态则根据鼠标位移拖动窗口
        /// </summary>
        private void dragThreadFunc()
        {
            while (true)
            {
                Thread.Sleep(10);
                if (isDrag)
                {
                    System.Drawing.Point mp = GetMousePose();

                    Dispatcher.Invoke(new Action(() =>
                    {
                        this.Left = winStartLeft + (mp.X - dragStartX) / UI_SCALE;
                        this.Top = winStartTop + (mp.Y - dragStartY) / UI_SCALE;
                    }));
                }
            }
        }

        private Action<JSONNode> ActDealMsg; //用于处理显示msg modal之后，点击按钮 异步事件的Action


        public MainWindow()
        {
            InitializeComponent();
            InitializeAsync();
            Thread thread = new Thread(dragThreadFunc);
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// 初始化WebView2
        /// </summary>
        async void InitializeAsync()
        {
            //加载之前需等待webview2 initialized完成。
            await webView.EnsureCoreWebView2Async(null);

            //webview2加载资源的几种方式，也可以在XAML里设定source，可以使用1,2两种方法
            //1. 直接通过URL加载网络页面
            //webView.Source = new Uri("https://localhost:44380");

            //2. 通过路径加载本地页面
            webView.Source = new Uri("file:///" + System.Environment.CurrentDirectory + "\\app\\index.html");

            //3. 将本地路径设为虚拟域名，通过虚拟域名加载本地页面
            //请注意，SetVirtualHostNameToFolderMapping这个方法需等待webview初始化完成，否则会因webview为空而崩溃，
            //所以不能放在MainWindow之中同步执行，会卡死，也不能放在wvMain.CoreWebView2InitializationCompleted和NavigationCompleted之中执行，
            //因为这两个方法都是加载完页面之后才会触发，如果xaml之中source为空，则永不会被执行，所以需在MainWindow异步执行并通过EnsureCoreWebView2Async方法等待：
            //webView.CoreWebView2.SetVirtualHostNameToFolderMapping("appassets.example", "app", CoreWebView2HostResourceAccessKind.Allow);
            //webView.Source = new Uri("https://appassets.example/hello.html");

            //4. 直接加载html字符串  加载的字符串大小不能大于2m 
            //html之中资源的链接必须通过虚拟域名，资源放在跟执行程序同一个目录下也无法直接调用
            //string htmlContent = @"<img src='http://appassets.example/bg0.jpg'>";
            //string htmlContent = @"<h1>hello, world!</h1>";
            //webView.NavigateToString(htmlContent);

            //webview2控件设定透明度        
            webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            //下面这行语句可以禁用f5等快捷键 以阻止用户误触f5刷新页面
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            //禁止缩放界面比例
            webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            webView.NavigationCompleted += webView_NavigationCompleted;
            webView.CoreWebView2InitializationCompleted += webView_InitializationCompleted;
            //webView.NavigationStarting += EnsureHttps;
            //webView.NavigationCompleted += CancelRightButton;
            webView.WebMessageReceived += WebView_WebMessageReceived;           

        }
        async void webView_InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs args)
        {

        }

        /// <summary>
        /// WebView2加载完成之后，执行一些js语句
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        async void webView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            //一些使用js实现的功能
            //禁止鼠标右键菜单
            await webView.CoreWebView2.ExecuteScriptAsync("window.addEventListener('contextmenu', window => {window.preventDefault();});");
            //禁止鼠标左键拖动选择
            await webView.CoreWebView2.ExecuteScriptAsync("window.addEventListener('selectstart', window => {window.preventDefault();});");
            //禁止拖动文件到窗口
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                   "window.addEventListener('dragover',function(e){e.preventDefault();},false);" +
                   "window.addEventListener('drop',function(e){" +
                      "e.preventDefault();" +
                      "console.log(e.dataTransfer);" +
                      "console.log(e.dataTransfer.files[0])" +
                   "}, false);");
            //await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.postMessage(window.document.URL);");
            //await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.addEventListener(\'message\', event => alert(event.data));");
            //await webView.CoreWebView2.ExecuteScriptAsync($"alert('alertt');document.getElementById('hello').innerHTML='hello;");
        }

        /// <summary>
        /// 接收webview2发送的消息并处理，此处html发来的消息为json格式，使用SimpleJSON库解析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        async void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            var json = JSON.Parse(args.WebMessageAsJson);
            if (json["class"] == "msg")
            {
                if (ActDealMsg != null)
                {
                    ActDealMsg(json);
                }
            }
            else
            {
                if (json["act"] == "mouse_left_down") //如果在webview2之中点击html控件则会触发此事件，记录鼠标坐标和窗口坐标，开启拖动窗口功能
                {
                    System.Drawing.Point mp = GetMousePose();
                    dragStartX = mp.X;
                    dragStartY = mp.Y;
                    winStartLeft = (int)this.Left;
                    winStartTop = (int)this.Top;
                    isDrag = true;
                }
                else if (json["act"] == "mouse_left_up") //如果在webview2之中释放鼠标左键则关闭拖动窗口功能
                {
                    isDrag = false;
                }
                else if (json["act"] == "b_quit")
                {
                    //Base64ExtractMP4(json["data"], "01.mp4");
                    System.Windows.Application.Current.Shutdown();
                }
                else if(json["act"] == "b_login")
                {
                    var result = await webView.CoreWebView2.ExecuteScriptWithResultAsync("get_user_info()"); //直接调用html之中的js函数
                    var username = JSON.Parse(result.ResultAsJson)["username"];
                    var password = JSON.Parse(result.ResultAsJson)["password"];

                    //判断密码和用户名

                    //密码错误则显示错误信息 msg modal
                    await webView.CoreWebView2.ExecuteScriptAsync("show_msg();");

                    //为ActDealMsg赋值一个异步事件，用于处理msg modal之后，点击按钮的事件，执行完之后将ActDealMsg置为null
                    ActDealMsg += async (JSONNode jsonT) =>
                    {
                        if (jsonT["act"] == "b_msg_close") //如果点了msg modal的按钮，执行对应操作
                        {

                        }
                        //关闭msg modal
                        await webView.CoreWebView2.ExecuteScriptAsync("close_msg();");
                        ActDealMsg = null;
                    };
                    return;
                }
            }
        }


        /// <summary>
        /// 或者不适使用SimpleJSON库，而是使用微软官方示例之中的message，具体可以参考微软官方示例
        /// </summary>
        /// <param name="args"></param>
        void HandleWebMessage(CoreWebView2WebMessageReceivedEventArgs args)
        {
            string message = args.TryGetWebMessageAsString();

            if (message.Contains("bQuit"))
            {
                //int msgLength = "SetTitleText".Length;
                //this.Title = message.Substring(msgLength);
                System.Windows.Application.Current.Shutdown();
            }
            else if (message.Contains("bLogin"))
            {
                webView.Source = new Uri("file:///" + System.Environment.CurrentDirectory + "\\App\\info.html");
            }
            else if (message.Contains("bRefresh"))
            {
                ExecuteScriptInfo();
            }
        }

        async void ExecuteScriptInfo()
        {
            string text = "document.getElementById(\"tlog\").innerHTML=\"!!!!!\";";// System.IO.File.ReadAllText(@"C:\PATH_TO_YOUR_FILE\script.js");
            await webView.CoreWebView2.ExecuteScriptAsync(text);
        }


        void EnsureHttps(object sender, CoreWebView2NavigationStartingEventArgs args)
        {
            String uri = args.Uri;
            if (!uri.StartsWith("https://"))
            {
                webView.CoreWebView2.ExecuteScriptAsync($"alert('{uri} is not safe, try an https link')");
                args.Cancel = true;
            }
        }

        void CancelRightButton(object sender, EventArgs args)
        {
            webView.CoreWebView2.ExecuteScriptAsync("window.addEventListener('contextmenu', window => {window.preventDefault();});");
        }


        void UpdateAddressBar(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {

            String uri = args.TryGetWebMessageAsString();
            //addressBar.Text = uri;
            webView.CoreWebView2.PostWebMessageAsString(uri);
        }

        /// <summary>
        /// 用于获取从html之中传来的h264格式的视频并保存到本地
        /// </summary>
        /// <param name="inputText"></param>
        /// <param name="fileName"></param>
        public static void Base64ExtractMP4(string inputText, string fileName)
        {
            //匹配mp4的正则表达式  data:video/webm;codecs=h264;base64,GkXfo6NChoEBQveBAULygQRC84EIQoKIbWF0cm9za2FCh4EEQoWBAhhTgGcB/////////
            string pattern = @"data:video/(?<type>.+?);codecs=h264;base64,(?<data>[^""]+)";
            Regex regex = new Regex(pattern, RegexOptions.Compiled);
            MatchCollection matches = regex.Matches(inputText);
            int index = 0;
            foreach (Match match in matches)
            {
                string type = match.Groups["type"].Value;
                string data = match.Groups["data"].Value;
                byte[] bytes = Convert.FromBase64String(data);
                if (!Directory.Exists("camera/out"))
                {
                    Directory.CreateDirectory("camera/out");
                }


                //string fileName = "image_" + index.ToString() + "." + type;
                using (FileStream fs = new FileStream("camera/out/" + fileName, FileMode.Create))
                {
                    fs.Write(bytes, 0, bytes.Length);
                }
                index++;
            }
        }

        /// <summary>
        /// 用于获取从html之中传来的base64格式的图片并保存到本地
        /// </summary>
        /// <param name="inputText"></param>
        /// <param name="fileName"></param>
        public static void Base64Extract(string inputText, string fileName)
        {
            //匹配所有base64格式图片的正则表达式
            string pattern = @"data:image/(?<type>.+?);base64,(?<data>[^""]+)";
            Regex regex = new Regex(pattern, RegexOptions.Compiled);
            MatchCollection matches = regex.Matches(inputText);
            int index = 0;
            foreach (Match match in matches)
            {
                string type = match.Groups["type"].Value;
                string data = match.Groups["data"].Value;
                byte[] bytes = Convert.FromBase64String(data);
                if (!Directory.Exists("App/img/out"))
                {
                    Directory.CreateDirectory("App/img/out");
                }


                //string fileName = "image_" + index.ToString() + "." + type;
                using (FileStream fs = new FileStream("App/img/out/" + fileName, FileMode.Create))
                {
                    fs.Write(bytes, 0, bytes.Length);
                }
                index++;
            }
        }



        private void bHello_Click(object sender, RoutedEventArgs e)
        {
            //webView.CoreWebView2.PostWebMessageAsJson("{\"SayText\":\"blue\"}");
            webView.CoreWebView2.PostWebMessageAsString("HELLO");
        }

        private async void bAlert_Click(object sender, RoutedEventArgs e)
        {
            string text = "alert('helloe!');";
            await webView.CoreWebView2.ExecuteScriptAsync(text);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        int CountNum = 0;



        public static Action<JSONNode> ActMsg;



        private async void bDoSomething_Click(object sender, RoutedEventArgs e)
        {
            //double milliseconds = DateTime.Now.TimeOfDay.TotalMilliseconds;
            //Console.WriteLine(DateTime.Now.ToString($"dd HH:mm:ss {milliseconds} 点击按钮 {CountNum}"));
            //Dosomething(CountNum);
            //CountNum++;


            // <ExecuteScriptWithResult>

            //string cmd = "function hello(){alert('hello');}";

            //await webView.CoreWebView2.ExecuteScriptAsync(cmd);



            var result = await webView.CoreWebView2.ExecuteScriptWithResultAsync("get_user_info()");
            var jsonO = JSON.Parse(result.ResultAsJson);

            //Console.WriteLine(jsonO);
            return;
            await webView.CoreWebView2.ExecuteScriptAsync("show_msg('hello world');");

            ActMsg += (JSONNode json) =>
            {
                Console.WriteLine(json["act"].Value);
                ActMsg = null;
            };



            //var result = await webView.CoreWebView2.ExecuteScriptWithResultAsync("show_msg('hello world');");

            //var json = JSON.Parse(result.ResultAsJson);
            //Console.WriteLine(json["act"].Value);
            //Console.WriteLine(json["page"].Value);
            //Console.WriteLine(json["pos"].Value);


            //if (result.Succeeded)
            //{
            //    MessageBox.Show(this, result.ResultAsJson, "ExecuteScript Json Result");
            //    int is_success = 0;
            //    string str = "";
            //    result.TryGetResultAsString(out str, out is_success);
            //    if (is_success == 1)
            //    {
            //        MessageBox.Show(this, str, "ExecuteScript String Result");
            //    }
            //    else
            //    {
            //        MessageBox.Show(this, "Get string failed", "ExecuteScript String Result");
            //    }
            //}
            //else
            //{
            //    var exception = result.Exception;
            //    MessageBox.Show(this, exception.Name, "ExecuteScript Exception Name");
            //    MessageBox.Show(this, exception.Message, "ExecuteScript Exception Message");
            //    MessageBox.Show(this, exception.ToJson, "ExecuteScript Exception Detail");
            //    var location_info = "LineNumber:" + exception.LineNumber + ", ColumnNumber:" + exception.ColumnNumber;
            //    MessageBox.Show(this, location_info, "ExecuteScript Exception Location");
            //}
            //// </ExecuteScriptWithResult>

            return;


            string methodName = "Runtime.evaluate";
            string methodParams = "{\"expression\":\"alert('test')\"}";
            try
            {
                string cdpResult = await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(methodName, methodParams);
                MessageBox.Show(this, cdpResult, "CDP method call successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "CDP method call failed");
            }
        }

        static TickLimiter tickLimiter = new TickLimiter();
        static void Dosomething(int num)
        {
            if (tickLimiter.Limiting("abc_123_@#$", 1000))
            {
                return;
            }
            double milliseconds = DateTime.Now.TimeOfDay.TotalMilliseconds;
            Console.WriteLine(DateTime.Now.ToString($"dd HH:mm:ss {milliseconds}  =====执行具体事务======= {num}"));
        }


        public class TickLimiter
        {
            readonly Dictionary<string, uint> dict = new Dictionary<string, uint>();
            uint prev;
            readonly object _lock = new object();
            /// <summary>
            /// 判断flag操作是否在milliseconds间隔限制内
            /// </summary>
            public bool Limiting(string flag, uint milliseconds)
            {
                uint tc = (uint)Environment.TickCount; //4294967295/1000/86400=49.71天后就会从0开始
                if (prev > tc)
                { //如果前一个值大于当前值，就说明从0开始了
                    lock (_lock)
                    {
                        if (prev > tc)
                        {
                            dict.Clear(); //清空所有记录，避免永远处于限制状态
                        }
                    }
                }
                prev = tc;
                if (!dict.ContainsKey(flag))
                {
                    dict.Add(flag, tc + milliseconds);
                    return false;
                }
                if (tc < dict[flag])
                { //当前时间未超过上次指定的限制时间就认为是限制中
                    return true;
                }
                dict[flag] = tc + milliseconds;
                return false;
            }
        }

    }
}
