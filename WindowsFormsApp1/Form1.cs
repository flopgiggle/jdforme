using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows.Forms;
using CsQuery;
using FluentScheduler;
using MsieJavaScriptEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static WindowsFormsApp1.EnumType;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        //是否已经处理过拍卖完成的检查事件
        private readonly Dictionary<string, bool> _isPaimaiFinishChecked = new Dictionary<string, bool>();
        /// <summary>
        /// 记录拍卖id，与最高拍卖价格的关系
        /// </summary>
        private readonly Hashtable _paimaiPrice = new Hashtable();
        /// <summary>
        /// 新增日志队列,增加在高并发下的日志处理 异常日志队列，全局唯一
        /// </summary>
        public static Queue<string> LogInfoQueue = new Queue<string>();

        private readonly Dictionary<Schedule, PaimaiItem> _scheduleAndPaimaiInfo =
            new Dictionary<Schedule, PaimaiItem>();

        //string password = "855133";
        //string jdName1 = "13689001585";
        private List<AccountInfo> _accountsList;

        private Dictionary<string, LoginBaseInfo> _userInfoList = new Dictionary<string, LoginBaseInfo>();

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            //加载会话
            button5_Click(null, null);
            //开启爬虫

            //StartJobNow(() => { button2_Click(null, null); }, 10);

            //开启数据统计
            //StartJobNow(() => { button6_Click(null, null);; }, 15);

            //开启延迟校准
            //StartJobNow(() => { button7_Click(null, null); ; }, 20);

            //展示所有job
            ShowAllPaimaiJob();

            ShowLog();
        }

        private void AddLog(string logInfo)
        {
            LogInfoQueue.Enqueue(logInfo);
        }

        private void ShowLog()
        {
            //开启一个线程扫描日志队列
            ThreadPool.QueueUserWorkItem(a =>
            {
                while (true)
                {
                    if (LogInfoQueue.Any())
                    {
                        var log = LogInfoQueue.Dequeue();
                        if (log != null)
                        {
                            richTextBox1.AppendText(log);//.AddLog(log);
                        }
                        else
                        {
                            Thread.Sleep(1000);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
                // ReSharper disable once FunctionNeverReturns
            });
        }


        private void LoadAllAccountList()
        {
            _userInfoList = new Dictionary<string, LoginBaseInfo>();
            //获取帐号密码列表
            var accounts = Config.userAccout.Split('&');
            _accountsList = new List<AccountInfo>();
            var virtualKey = new Queue();
            for (var i = 0; i < 100; i++)
            {
                virtualKey.Enqueue(i);
            }

            foreach (string t in accounts)
            {
                var account = t.Split('|')[0];
                var password = t.Split('|')[1];
                for (var j = 0; j < Config.xloginNum; j++)
                {
                    _accountsList.Add(new AccountInfo
                    {
                        realAccount = account,
                        password = password,
                        virtualAccount = virtualKey.Dequeue() + "-" + account
                    });
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var cooikes = JsonConvert.SerializeObject(SessionDic.userCookies);
            File.WriteAllText("cooikes.txt", cooikes);
            richTextBox3.AppendText("\n登陆信息已保存到本地");
        }

        private void button5_Click(object sender, EventArgs e)
        {
            LoadAllAccountList();
            var cooikes = File.ReadAllText("cooikes.txt");
            SessionDic.userCookies = DeserializeJsonToObject<Dictionary<string, string>>(cooikes);
            richTextBox3.AppendText("\n登陆信息加载成功");
        }

        private void button7_Click(object sender, EventArgs e)
        {
            //button2_Click(null,null);
            //richTextBox5.AppendText("开始爬取数据:" + getNow());
            JobManager.AddJob(() =>
            {
                //查找距离目前结束时间大于5秒的数据
                if (SessionDic.CurrentAuctionList.Count > 0)
                {
                    var item = SessionDic.CurrentAuctionList.FirstOrDefault();
                    if (item != null)
                    {
                        var startTime = ConvertIntDateTime(item.endTime);
                        //if ((startTime - System.DateTime.Now).Seconds < 15) {
                        richTextBox5.AppendText("\n预计结束时间:" + GetTime(ConvertIntDateTime(item.endTime)) + " 当前时间:" +
                                                GetNow());
                        //开始分析
                        StartJobNow(() =>
                        {
                            //richTextBox5.AppendText("\n开始测试: 当前时间:" + getNow());
                            TimeFixTest(item.paimaiId);
                        }, startTime.AddMilliseconds(-Config.beforeBeginTime));
                    }
                    //}
                }
            }, t => { t.ToRunNow().AndEvery(120).Seconds(); });
        }

        private static void StartJobNow(Action job, DateTime startTime)
        {
            //开启定时任务
            JobManager.AddJob(job, t => { t.ToRunOnceAt(startTime); });
        }

        private void StartJobNow(Action job, int witchSecendToStart)
        {
            //开启定时任务
            JobManager.AddJob(job, t => { t.ToRunOnceIn(witchSecendToStart).Seconds(); });
        }

        private void button8_Click(object sender, EventArgs e)
        {
            Config.beforeBeginTime = int.Parse(textBox1.Text);
        }

        #region 登陆相关

        private void BTLogin_Click(object sender, EventArgs e)
        {
            LoadAllAccountList();
            SessionDic.userCookies = new Dictionary<string, string>();
            var k = 0;
            _accountsList.ForEach(a =>
            {
                k++;
                JobManager.AddJob(() =>
                {
                    richTextBox3.AppendText("\n" + a.virtualAccount + "开始登陆");
                    Login(a);
                }, t =>
                {
                    //t.ToRunNow().AndEvery(1).Seconds();
                    var r = new Random();
                    var startTime = 1;
                    if (_accountsList.Count > 1)
                    {
                        startTime = r.Next(k + k, k * 3);
                    }

                    t.ToRunOnceAt(DateTime.Now.AddSeconds(startTime));
                });
            });
        }

        private bool IsNeedAuthCode(AccountInfo accountInfo)
        {
            var httphelper = new HttpHelper(true, accountInfo.virtualAccount);
            var item = GetHttpItem();
            item.URL = "https://passport.jd.com/uc/showAuthCode";
            item.Method = "post";
            item.Postdata = "{'loginName':" + accountInfo.realAccount + "}";
            item.PostDataType = PostDataType.String;
            item.ResultType = ResultType.String;
            //item.ContentType = "application/json";
            item.Accept = "application/json";
            var header = new WebHeaderCollection {{"r", new Random().Next().ToString()}, {"version", "2015"}};
            item.Header = header;
            var rs = httphelper.GetHtml(item);
            var result = JObject.Parse(rs.Html.Replace("(", "").Replace(")", ""));
            if (result["verifycode"].Value<string>() != "True")
            {
                return false;
            }

            return true;
        }

        /// <summary>
        ///     登陆
        /// </summary>
        private void Login(AccountInfo accountInfo)
        {
            _userInfoList[accountInfo.virtualAccount] = GetLoginBaseInfo(accountInfo);
            if (!IsNeedAuthCode(accountInfo))
            {
                PostLoginInfo(_userInfoList[accountInfo.virtualAccount], accountInfo);
            }
            else
            {
                richTextBox3.AppendText("\n" + accountInfo.virtualAccount + "获取验证码");
                var image = GetImage(_userInfoList[accountInfo.virtualAccount].YzmUrl, accountInfo);
                AddImageInfoArea(image, accountInfo);
            }
        }

        private void AddImageInfoArea(Image image, AccountInfo accountInfo)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(delegate
                {
                    var panel = new FlowLayoutPanel
                    {
                        FlowDirection = FlowDirection.LeftToRight,
                        Height = 100,
                        Width = 500
                    };

                    var pic = new PictureBox
                    {
                        BackgroundImage = image,
                        BackgroundImageLayout = ImageLayout.Stretch,
                        SizeMode = PictureBoxSizeMode.StretchImage,
                        Height = 90,
                        Width = 200
                    };


                    var tb = new TextBox();
                    _userInfoList[accountInfo.virtualAccount].YzmControl = tb;
                    tb.Name = accountInfo.virtualAccount + "|" + controlerType.textBox;
                    var button = new Button
                    {
                        Text = accountInfo.virtualAccount + @"|确认",
                        Name = accountInfo.virtualAccount + "|" + controlerType.button
                    };
                    panel.Controls.Add(pic);
                    panel.Controls.Add(tb);
                    panel.Controls.Add(button);
                    flowLayoutPanel1.Controls.Add(panel);
                    button.Click += TBConfirmYZM_Click;
                }));
            }
        }

        private void TBConfirmYZM_Click(object sender, EventArgs e)
        {
            var button = (Button) sender;
            var account = button.Name.Split('|')[0];
            var yzmText = _userInfoList[account].YzmControl;
            _userInfoList[account].PostInfo += yzmText.Text;
            var accountInfo = _accountsList.First(a => a.virtualAccount == account);
            PostLoginInfo(_userInfoList[account], accountInfo);
        }

        private void PostLoginInfo(LoginBaseInfo loginBaseInfo, AccountInfo accountInfo)
        {
            var httphelper = new HttpHelper(true, accountInfo.virtualAccount);
            var item = GetHttpItem();
            item.PostDataType = PostDataType.String;
            item.Method = "post";
            item.Accept = "text/plain, */*; q=0.01";
            item.Host = "passport.jd.com";
            item.URL = loginBaseInfo.LoginUrl;
            item.Referer = "https://passport.jd.com/new/login.aspx";
            item.Postdata = loginBaseInfo.PostInfo; // item.Postdata.Replace("+", "%20");

            item.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            item.PostEncoding = Encoding.UTF8;
            item.IsXRequestedWith = true;
            var loginResult = httphelper.GetHtml(item);
            if (loginResult.Html.ToLower().Contains("success"))
            {
                richTextBox3.AppendText("\n" + accountInfo.virtualAccount + "登陆成功");
            }
            else
            {
                if (loginResult.Html.ToLower().Contains("authcode"))
                {
                    richTextBox3.AppendText("\n" + accountInfo.virtualAccount + "验证码不正确或已过期");
                }
                else
                {
                    richTextBox3.AppendText("\n" + accountInfo.virtualAccount + "登陆失败");
                }

                richTextBox3.AppendText("\n" + accountInfo.virtualAccount + "重新开始登陆");
                Login(accountInfo);
            }
            //表示StatusCode的文字说明与描述
            //string statusCodeDescription = rs.StatusDescription;
        }

        private LoginBaseInfo GetLoginBaseInfo(AccountInfo accountInfo)
        {
            var httphelper = new HttpHelper(true, accountInfo.virtualAccount);

            var data = new Dictionary<string, string>();

            var item = GetHttpItem();
            item.URL = "https://passport.jd.com/new/login.aspx";

            var rs = httphelper.GetHtml(item);


            //返回的Html内容
            var html = rs.Html;
            if (!html.Contains("欢迎登录"))
            {
                Thread.Sleep(4000);
                rs = httphelper.GetHtml(item);
                html = rs.Html;
            }
            //var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            //htmlDoc.Load(html);
            //htmlDoc.SelectNodes

            CQ dom = html;
            var bold = dom["form#formlogin input[type=hidden]"];
            var yzmUrl = "https://" + dom["img#JD_Verification1"][0].Attributes["src2"].Replace("//", "") +
                         "&yys=1495705227054";
            //var yzmUrl = dom["img#JD_Verification1"][0].Attributes["src"].Replace("//", "");

            var hiddenInputList = bold.Select(a => new {name = a.Attributes["name"], value = a.Attributes["value"]})
                .ToList();
            hiddenInputList.ForEach(a => { data.Add(a.name, a.value); });
            if (!hiddenInputList.Exists(a => a.name == "pubKey"))
            {
                var publicKey = dom["input[name='pubKey']"];
                var publicKeyItem = publicKey
                    .Select(a => new {name = a.Attributes["name"], value = a.Attributes["value"]}).First();
                hiddenInputList.Add(publicKeyItem);
            }

            var jdLoginJs = File.ReadAllText("./jd_login.js");
            var pubKey = hiddenInputList.Find(a => a.name == "pubKey").value;
            data["pubKey"] = pubKey;
            string nloginpwd;
            jdLoginJs += ";get('" + pubKey + "','" + accountInfo.password + "');";
            using (var jsEngine = new MsieJsEngine())
            {
                nloginpwd = jsEngine.Evaluate<string>(jdLoginJs);
            }
            data["loginname"] = accountInfo.realAccount;
            data["nloginpwd"] = nloginpwd;
            //var ss = ;
            var eid = "GEOVEU4EP2CNEUQ3JFZENF2YARUJWXZZSBEGHDDWMYPXDIROG73DGBOELY3GMEAGGB427RQ5T5O2G3ODOSUODUMEUQ";
            var fp = "c675b7cca9e276a3bca88c407cb14dbb";

            item.Postdata = "uuid=" + Encode(data["uuid"]) + "&" + "eid=" + Encode(eid) + "&" + "fp=" + fp + "&" +
                            "_t=" + Encode(data["_t"]) + "&" +
                            "loginType=f" + "&" + "loginname=" + accountInfo.realAccount + "&" + "nloginpwd=" +
                            accountInfo.password + "&" + "chkRememberMe=" + "&" +
                            "authcode=";

            var loginUrl = "https://passport.jd.com/uc/loginService?" + "uuid=" + Encode(data["uuid"]) + "&" + "r=" +
                           new Random().NextDouble() + "&" + "version=2015";
            return new LoginBaseInfo {PostInfo = item.Postdata, YzmUrl = yzmUrl, LoginUrl = loginUrl};
        }

        #endregion

        #region 抓取数据相关

        private void button2_Click(object sender, EventArgs e)
        {
            //开启定时任务
            JobManager.AddJob(() =>
            {
                richTextBox4.Text = "\n" + "抓取首页数据:" + GetNow();
                DbdIndexSpider();
            }, t =>
            {
                //t.ToRunNow().AndEvery(1).Seconds();
                t.ToRunNow().AndEvery(1).Minutes();
            });
        }

        /// <summary>
        ///     获取当前页面最新正在拍卖的商品列表
        /// </summary>
        /// <returns></returns>
        private List<PaimaiItem> GetCurrentPaimaiInfo()
        {
            var httphelper = new HttpHelper(true);
            var httpItem = GetPostItem();
            httpItem.URL = "https://dbd.jd.com/auctionList.html";
            httpItem.Referer = "https://dbd.jd.com/auctionList.html";
            //httpItem. = "https://dbd.jd.com";
            httpItem.Postdata =
                "useStatus=&auctionType=&sortField=2&recordType=&searchForTitle=&productCateId=&productLocation=&searchParam=";
            var result = httphelper.GetHtml(httpItem);
            CQ dom = result.Html;
            var paimaiList = new List<PaimaiItem>();
            dom[".auctionList li .name a"].Each(a => paimaiList.Add(new PaimaiItem
            {
                paimaiId = GetIdByDetailUrl(a.Attributes["href"]),
                detailUrl = a.Attributes["href"],
                describe = Deunicode(a.InnerHTML)
            }));
            return paimaiList;
        }

        /// <summary>
        ///     获取拍卖信息详情
        /// </summary>
        /// <param name="paimaiItem"></param>
        /// <returns></returns>
        private List<PaimaiItem> GetPaiMaiDetailListFromWeb(List<PaimaiItem> paimaiItem)
        {
            var httphelper = new HttpHelper(true);
            var dataparams = "";
            var httpItem = GetHttpItem();
            paimaiItem.ForEach(a => dataparams += a.paimaiId + "-");
            httpItem.URL = "https://dbditem.jd.com/services/currentList.action?paimaiIds=" + dataparams + "&callback=j";
            var result = httphelper.GetHtml(httpItem);
            if (result.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            var webJsonItem = JArray.Parse(result.Html.Substring(2, result.Html.Length - 3));
            foreach (JToken t in webJsonItem)
            {
                if (t.ToString() == "")
                {
                    continue;
                }

                var webItem = t;
                var paimaiId = webItem["paimaiId"].Value<string>();
                var paimai = paimaiItem.Find(a => a.paimaiId == paimaiId);
                paimai.productId = webItem["productId"].Value<string>();
                paimai.auctionStatus = webItem["auctionStatus"].Value<int>();
                paimai.bidCount = webItem["bidCount"].Value<int>();
                paimai.startTime = webItem["startTime"].Value<long>();
                paimai.endTime = webItem["endTime"].Value<long>();
                paimai.currentPrice = webItem["currentPrice"].Value<double>();
                paimai.remainTime = webItem["remainTime"].Value<long>();
            }
            return paimaiItem;
        }

        #endregion

        #region 统计与保存数据

        /// <summary>
        ///     点击统计服务按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button6_Click(object sender, EventArgs e)
        {
            //开启定时任务
            JobManager.AddJob(() =>
            {
                //AddLog("\n" + "开始统计数据:" + getNow());
                var count = CalculateProductInfoForBach();
                richTextBox4.Text ="\n" + "完成统计数据:" + count + "条" + GetNow();
            }, t =>
            {
                //t.ToRunNow().AndEvery(1).Seconds();
                t.ToRunNow().AndEvery(60).Seconds();
            });
        }

        private void DbdIndexSpider()
        {
            var list = GetCurrentPaimaiInfo();
            list.ForEach(a => { });
            //richTextBox2.Text = listString;
            SessionDic.CurrentAuctionList = list;
            SavePaiMaiDetailList(list);
        }

        /// <summary>
        ///     保存拍卖明细数据
        /// </summary>
        /// <param name="paimaiItem"></param>
        /// <returns></returns>
        private void SavePaimaiDetail(PaimaiItem paimaiItem)
        {
            var db = new jd();

            //检查产品是否存在
            var productList = db.products.Where(pro => pro.productId == paimaiItem.productId);
            //如果不存在则添加
            if (!productList.Any())
            {
                db.products.Add(new product
                {
                    productId = paimaiItem.productId,
                    name = paimaiItem.describe,
                    auctionCount = 0,
                    createTime = DateTime.Now
                });
                db.SaveChanges();
            }
            else
            {
                var oneProduct = productList.Single();
                if (!string.IsNullOrEmpty(paimaiItem.describe) &&
                    string.IsNullOrEmpty(oneProduct.name))
                {
                    oneProduct.name = paimaiItem.describe;
                    oneProduct.updateTime = DateTime.Now;
                    db.SaveChanges();
                }
            }
            //检查拍卖编号是否存在
            var auction = db.auctions.Where(pro => pro.auctionId == paimaiItem.paimaiId);
            if (!auction.Any())
            {
                db.auctions.Add(new auction
                {
                    currentPrice = (decimal) paimaiItem.currentPrice,
                    productId = paimaiItem.productId,
                    auctionId = paimaiItem.paimaiId,
                    startTime = paimaiItem.startTime,
                    endTime = paimaiItem.endTime,
                    bidCount = paimaiItem.bidCount,
                    auctionStatus = paimaiItem.auctionStatus,
                    createTime = DateTime.Now
                });
                var product = db.products.First(pro => pro.productId == paimaiItem.productId);
                product.auctionCount = product.auctionCount + 1;
                db.SaveChanges();
            }
            else
            {
                var auctionItem = auction.Single();
                auctionItem.auctionStatus = paimaiItem.auctionStatus;
                auctionItem.currentPrice = (decimal) paimaiItem.currentPrice;
                auctionItem.endTime = paimaiItem.endTime;
                auctionItem.startTime = paimaiItem.startTime;
                auctionItem.bidCount = paimaiItem.bidCount;
                auctionItem.updateTime = DateTime.Now;
                db.SaveChanges();
            }
        }

        private int CalculateProductInfoForBach()
        {
            //查询数据库中，未完成的所有拍卖
            var db = new jd();
            //当前时间减去一分钟，避免时间不同步造成的延迟问题
            var currentTimeStamp = long.Parse((long) ConvertDateTimeInt(DateTime.Now.AddSeconds(-30)) + "000");
            //获取40条，竞拍时间已经结束，但竞拍状态还未重置的数据
            var auctions = db.auctions
                .Where(a => a.endTime < currentTimeStamp && a.auctionStatus == (int) AuctionStatus.Auctioning).Take(40)
                .ToList();
            if (auctions.Count == 0)
            {
                return 0;
            }
            //联网查询最新的拍卖数据
            var processlist = auctions.Select(a => new PaimaiItem {paimaiId = a.auctionId}).ToList();
            var webList = GetPaiMaiDetailListFromWeb(processlist);
            if (webList == null)
            {
                return -1;
            }

            webList.Where(a => a.auctionStatus == 0).ToList().ForEach(a =>
            {
                var auction = db.auctions.Single(b => a.paimaiId == b.auctionId);
                auction.auctionStatus = (int) AuctionStatus.ErrorNotFoundAuction;
                //MessageBox.Show("");
            });
            //if()
            db.SaveChanges();
            //处理每一条返回的数据
            var list = webList.Where(a => a.auctionStatus != 0).ToList();
            list.ForEach(CalculateProductInfo);
            return list.Count;
        }

        //联网查询产品价格
        private double GetProductPrice(string productId, string paimaiId)
        {
            var item = GetHttpItem();
            item.URL = "https://dbditem.jd.com/json/current/queryJdPrice?sku=" + productId + "&paimaiId=" + paimaiId;
            var helper = new HttpHelper();
            try
            {
                var html = helper.GetHtml(item).Html;
                return JObject.Parse(html)["jdPrice"].Value<double>();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        ///     拍卖结束后，求出对应产品的最新成交价格，历史评价价格，最低价，最高价
        /// </summary>
        /// <param name="paimaiItem"></param>
        /// <returns></returns>
        private void CalculateProductInfo(PaimaiItem paimaiItem)
        {
            var price = GetProductPrice(paimaiItem.productId, paimaiItem.paimaiId);

            var db = new jd();
            if ((int)price != 0)
            {
                db.productPrices.Add(new productPrice
                {
                    price = (decimal) price,
                    productId = paimaiItem.productId,
                    createTime = DateTime.Now
                });
            }


            //检查产品是否存在
            var productList = db.products.Single(pro => pro.productId == paimaiItem.productId);
            var auction = db.auctions.Single(pro => pro.auctionId == paimaiItem.paimaiId);
            //productList.auctionEndCount = productList.auctionEndCount + 1;
            if (productList.auctionAvgPrice == null)
            {
                productList.auctionAvgPrice = (decimal) paimaiItem.currentPrice;
            }
            else
            {
                productList.auctionAvgPrice =
                    (productList.auctionEndCount * productList.auctionAvgPrice + (decimal) paimaiItem.currentPrice) /
                    (productList.auctionEndCount + 1);
            }

            if (productList.auctionEndCount == null)
            {
                productList.auctionEndCount = 1;
            }
            else
            {
                productList.auctionEndCount = productList.auctionEndCount + 1;
            }

            if (productList.auctionTopPrice == null)
            {
                productList.auctionTopPrice = (decimal) paimaiItem.currentPrice;
            }
            else if (productList.auctionTopPrice < (decimal) paimaiItem.currentPrice)
            {
                productList.auctionTopPrice = (decimal) paimaiItem.currentPrice;
            }

            if (productList.auctionLowPrice == null)
            {
                productList.auctionLowPrice = (decimal) paimaiItem.currentPrice;
            }
            else if (productList.auctionLowPrice > (decimal) paimaiItem.currentPrice)
            {
                productList.auctionLowPrice = (decimal) paimaiItem.currentPrice;
            }

            if ((int)price != 0)
            {
                productList.price = (decimal) price;
            }

            productList.updateTime = DateTime.Now;
            auction.auctionStatus = (int) AuctionStatus.EndAuction;
            auction.updateTime = DateTime.Now;
            auction.currentPrice = (decimal) paimaiItem.currentPrice;
            auction.bidCount = paimaiItem.bidCount;
            db.SaveChanges();
        }

        /// <summary>
        ///     根据商品列表查询具体拍卖信息
        /// </summary>
        /// <returns></returns>
        private void SavePaiMaiDetailList(List<PaimaiItem> paimaiItem)
        {
            var detailList = GetPaiMaiDetailListFromWeb(paimaiItem);
            if (detailList == null)
            {
                return;
            }

            foreach (PaimaiItem t in detailList)
            {
                SavePaimaiDetail(t);
            }
        }

        #endregion

        #region 共通方法

        /// <summary>
        ///     解析JSON字符串生成对象实体
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="json">json字符串(eg.{"ID":"112","Name":"json"})</param>
        /// <returns>对象实体</returns>
        public static T DeserializeJsonToObject<T>(string json) where T : class
        {
            var serializer = new JsonSerializer();
            var sr = new StringReader(json);
            var o = serializer.Deserialize(new JsonTextReader(sr), typeof(T));
            var t = o as T;
            return t;
        }

        /// <summary>
        ///     反序列化JSON到给定的匿名对象.
        /// </summary>
        /// ///
        /// <typeparam name="T">匿名对象类型</typeparam>
        /// <param name="json">json字符串</param>
        /// <param name="anonymousTypeObject">匿名对象</param>
        /// <returns>匿名对象</returns>
        public static T DeserializeAnonymousType<T>(string json, T anonymousTypeObject)
        {
            var t = JsonConvert.DeserializeAnonymousType(json, anonymousTypeObject);
            return t;
        }

        private string GetTime(DateTime time)
        {
            return time.ToString(" HH:mm:ss.fff");
        }

        private string GetNow()
        {
            return GetTime(DateTime.Now);
        }

        private string GetIdByDetailUrl(string url)
        {
            var item = url.Split('/');
            return item[item.Length - 1];
        }

        public string Deunicode(string str)
        {
            var objRegex = new Regex("&#(?<UnicodeCode>[\\d]{5});", RegexOptions.IgnoreCase);
            var objMatch = objRegex.Match(str);
            var sb = new StringBuilder(str);
            while (objMatch.Success)
            {
                var code = Convert.ToString(Convert.ToInt32(objMatch.Result("${UnicodeCode}")), 16);
                var array = new byte[2];
                array[0] = (byte) Convert.ToInt32(code.Substring(2), 16);
                array[1] = (byte) Convert.ToInt32(code.Substring(0, 2), 16);

                sb.Replace(objMatch.Value, Encoding.Unicode.GetString(array));

                objMatch = objMatch.NextMatch();
            }
            return sb.ToString();
        }


        private HttpItem GetPostItem()
        {
            var item = GetHttpItem();
            item.PostDataType = PostDataType.String;
            item.Method = "post";
            item.Accept = "text/plain, */*; q=0.01";
            item.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            item.PostEncoding = Encoding.UTF8;
            item.IsXRequestedWith = true;
            return item;
        }

        private HttpItem GetHttpItem()
        {
            var item = new HttpItem
            {
                URL = "https://passport.jd.com/uc/login?ltype=logout", //URL     必需项
                Encoding = null, //编码格式（utf-8,gb2312,gbk）     可选项 默认类会自动识别
                //Encoding = Encoding.Default,
                Method = "get", //URL     可选项 默认为Get
                Timeout = 100000, //连接超时时间     可选项默认为100000
                ReadWriteTimeout = 30000, //写入Post数据超时时间     可选项默认为30000
                IsToLower = false, //得到的HTML代码是否转成小写     可选项默认转小写
                //Cookie = "",//字符串Cookie     可选项
                UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; WOW64)AppleWebKit/537.36 (KHTML, like Gecko) Chrome/56.0.2924.87 Safari/537.36", //用户的浏览器类型，版本，操作系统     可选项有默认值
                Accept = "text/html, application/xhtml+xml, */*", //    可选项有默认值
                ContentType = "text/html", //返回类型    可选项有默认值
                //Referer = "http://www.jd.com",//来源URL     可选项
                Allowautoredirect = true, //是否根据３０１跳转     可选项
                //CerPath = "d:\\123.cer",//证书绝对路径     可选项不需要证书时可以不写这个参数
                Connectionlimit = 1024, //最大连接数     可选项 默认为1024
                //Postdata = "C:\\PERKYSU_20121129150608_ScrubLog.txt",//Post数据     可选项GET时不需要写
                //PostDataType = PostDataType.FilePath,//默认为传入String类型，也可以设置PostDataType.Byte传入Byte类型数据

                ProxyIp =
                    Config.isSetProxy ? "127.0.0.1:8888" : "", //代理服务器ID 端口可以直接加到后面以：分开就行了    可选项 不需要代理 时可以不设置这三个参数
                //ProxyPwd = "123456",//代理服务器密码     可选项
                //ProxyUserName = "administrator",//代理服务器账户名     可选项
                ResultType = ResultType.String //返回数据类型，是Byte还是String
                //PostdataByte = System.Text.Encoding.Default.GetBytes("测试一下"),//如果PostDataType为Byte时要设置本属性的值
                //CookieCollection = new System.Net.CookieCollection(),//可以直接传一个Cookie集合进来
            };
            return item;
        }

        /// <summary>
        ///     字节数组生成图片
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns>图片</returns>
        private Image ByteArrayToImage(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var ms = new MemoryStream(bytes);
            return Image.FromStream(ms, true);
        }

        private Image GetImage(string imgUrl, AccountInfo account)
        {
            var http = new HttpHelper(true, account.virtualAccount);
            var item = GetHttpItem();
            item.URL = imgUrl;
            item.Method = "get";
            item.Accept = "image/webp,image/*,*/*;q=0.8";
            item.ResultType = ResultType.Byte;
            item.Referer = "https://passport.jd.com/new/login.aspx";
            var result = http.GetHtml(item);
            var img = ByteArrayToImage(result.ResultByte);
            return img;
        }

        /// <summary>
        ///     将Unix时间戳转换为DateTime类型时间
        /// </summary>
        /// <param name="d">double 型数字</param>
        /// <returns>DateTime</returns>
        public static DateTime ConvertIntDateTime(double d)
        {
            d = double.Parse(d.ToString(CultureInfo.InvariantCulture).Substring(0, 10));
            var startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            var time = startTime.AddSeconds(d);
            return time;
        }

        /// <summary>
        ///     将c# DateTime时间格式转换为Unix时间戳格式
        /// </summary>
        /// <param name="time">时间</param>
        /// <returns>double</returns>
        public static double ConvertDateTimeInt(DateTime time)
        {
            var startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            var intResult = (time - startTime).TotalSeconds;
            return intResult;
        }

        private string Encode(string item)
        {
            return HttpUtility.UrlEncode(item, Encoding.UTF8);
        }

        private string GetJobName(JobType type, string id)
        {
            return type + id;
        }

        #endregion
    }

    public class LoginBaseInfo
    {
        public string LoginUrl;
        public string PostInfo;
        public Control YzmControl;
        public string YzmUrl;
    }

    public class AuctionQuality
    {
        public string Facade { get; set; }
        public string Pakage { get; set; }
        public string Accessory { get; set; }
        public string Use { get; set; }
        //public auctionQualityType QualityType;
    }
}