# WebView2UI - 在WPF之中使用WebView2的一些经验总结

使用自定义的WindowBlur类配合WebView2实现了圆角毛玻璃半透明背景的浮窗，可以使用鼠标拖动，同时将WebView2常用的一些功能做了演示，详细说明参考XAML和CS文档。

App下的index.html和图片等资源文件放在Resource目录下。



webview简介与生命周期：[WPF 应用中的 WebView2 入门 - Microsoft Edge Developer documentation | Microsoft Learn](https://learn.microsoft.com/zh-cn/microsoft-edge/webview2/get-started/wpf)

具体代码可以参考微软官方示例文档 [WPF 示例应用 - Microsoft Edge Developer documentation | Microsoft Learn](https://learn.microsoft.com/zh-cn/microsoft-edge/webview2/samples/webview2wpfbrowser)



## 使用方法：

使用nuget添加Microsoft.Web.WebView2，将Resource目录下的app子目录复制到生成的可执行文件同一目录，xaml之中插入 xmlns:wv2="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf"

*如果需要使用webview2 prerelease版，需要在NuGet管理界面进入项目网页，然后在下面的Package Manager之中复制命令行到程序包管理控制台，在上面的下拉菜单之中选择要安装的项目，然后运行命令即可。*



## 初始化Webview2和几种加载html的方式

```C#
public MainWindow()
{
    InitializeComponent();
    InitializeAsync();
}

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
}
```





## 开发混合应用时用到的一些功能

```C#
async void InitializeAsync()
{
    //..... 初始化代码，省略，参考上面
    
    //webview2控件设定透明度        
    //webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

    //下面这行语句可以禁用f5等快捷键 以阻止用户误触f5刷新页面
    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
    //禁止缩放界面比例
    webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

    webView.NavigationCompleted += webView_NavigationCompleted;
    //webView.CoreWebView2InitializationCompleted += webView_InitializationCompleted;
    //webView.NavigationStarting += EnsureHttps;
    //webView.NavigationCompleted += CancelRightButton;
    webView.WebMessageReceived += WebView_WebMessageReceived; 
}

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
```





## C#在html之中插入脚本

```c#
await wView.CoreWebView2.ExecuteScriptAsync("some_func();");  //执行JS
var result = await webView.CoreWebView2.ExecuteScriptWithResultAsync("get_json();"); //执行带返回值的JS
string scriptResult = await _webViewFrames[iframeNumber].ExecuteScriptAsync(dialog.Input.Text);  //插入 iFrame
```

若html内容如下：

```javascript
var user_id = 120; //数字
var user_name = "hello"; //字符串
var user_info = {name: "hello",age: 12}; //json串
function get_user_name() {return user_name;}
function get_user_id() {return user_id;}
function get_user_info() {return user_info;}
```

使用SimpleJSON对返回数据进行处理：

```C#
var result_user_id = await webView.CoreWebView2.ExecuteScriptWithResultAsync("user_id");
//等效于 var result_user_id = await webView.CoreWebView2.ExecuteScriptWithResultAsync("get_user_id()");
long user_id = JSON.Parse(result_user_id.ResultAsJson).AsLong;//处理数值
//如果数值过大则会自动加双引号此时需long.TryParse(result.ResultAsJson.Replace("\"", ""), out t);

//处理字符串
var result_user_name = await webView.CoreWebView2.ExecuteScriptWithResultAsync("get_user_name();");
string user_name = JSON.Parse(result_user_name.ResultAsJson).Value;

//处理json串
var result_user_info = await webView.CoreWebView2.ExecuteScriptWithResultAsync("get_user_info();");
var json = JSON.Parse(result_user_info.ResultAsJson);//通过json["user_name"].Value 获取值 
```



## C#发送数据，html接收

C#发送：

```c#
webView.CoreWebView2.PostWebMessageAsJson(reply);
webView.CoreWebView2.PostWebMessageAsString(dialog.Input.Text);
```

html接收数据：

```javascript
window.chrome.webview.addEventListener('message', arg => {
   if ("WindowBounds" in arg.data) {
       document.getElementById("window-bounds").value = arg.data.WindowBounds;
   }
   if ("SetColor" in arg.data) {
       document.getElementById("colorable").style.color = arg.data.SetColor;
   }
});  
```



## html发送数据，C#接收

html发送：

```javascript
window.chrome.webview.postMessage("GetWindowBounds");
```

C#接收：

```c#
wvMain.WebMessageReceived += wvMain_WebMessageReceived;
```



## C#增加和删除js脚本

Enter the JavaScript code to run as the initialization script that runs before any script in the HTML document.

在任何js脚本之前运行

```C#
string scriptId = await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(dialog.Input.Text);
webView.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(scriptId);
```



