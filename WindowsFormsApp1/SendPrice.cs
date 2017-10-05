using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CsQuery;
using FluentScheduler;

namespace WindowsFormsApp1
{
    public partial class Form1
    {
        #region 出价相关

        private void TBProductId_Enter()
        {
            GetPaiMaiPromtInfo(TBProductId.Text);
        }

        private void GetPaiMaiPromtInfo(string productId)
        {
            var db = new jd();
            var product = db.products.FirstOrDefault(a => a.productId == productId);
            if (product?.auctionAvgPrice != null)
            {
                if (product.auctionTopPrice != null)
                {
                    if (product.auctionLowPrice != null)
                    {
                        label1.Text = @"当前:" + (int)(product.price ?? 0) + @"均:" +
                                      (int)product.auctionAvgPrice + @"均差:" +
                                      (int)(product.price == null
                                          ? 0
                                          : product.price - (int)product.auctionAvgPrice) +
                                      @" 高:" + (int)product.auctionTopPrice + @" 低:" + (int)product.auctionLowPrice +
                                      @" 次数:" +
                                      product.auctionEndCount;
                    }
                }

                TBMaxPrice.Text = ((int)product.auctionAvgPrice).ToString();
            }
            else
            {
                label1.Text = @"暂无数据";
            }
        }

        private void TBPaiMaiId_Enter()
        {
            var paimaiId = TBPaiMaiId.Text;
            var item = GetPaiMaiDetailListFromWeb(new List<PaimaiItem> { new PaimaiItem { paimaiId = paimaiId } });
            GetPaiMaiPromtInfo(item[0].productId);
        }

        private void TBPaiMaiId_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                JobManager.AddJob(TBPaiMaiId_Enter, t =>
                {
                    //t.ToRunNow().AndEvery(1).Seconds();
                    t.ToRunNow();
                });
                TBMaxPrice.Focus();
                TBProductId.Text = "";
            }
        }

        private void TBProductId_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                JobManager.AddJob(TBProductId_Enter, t =>
                {
                    //t.ToRunNow().AndEvery(1).Seconds();
                    t.ToRunNow();
                });
                TBMaxPrice.Focus();
                TBPaiMaiId.Text = "";
            }
        }

        private void TBMaxPrice_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button4_Click(null, null);
            }
        }

        /// <summary>
        ///     启动拍卖
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click(object sender, EventArgs e)
        {
            if (TBPaiMaiId.Text.Trim() == "" && TBProductId.Text.Trim() == "")
            {
                AddLog("\n" + "请至少输入一个产品信息");
                return;
            }

            if (TBMaxPrice.Text == "")
            {
                AddLog("\n" + "必须输入产品价格");
                return;
            }

            //拍卖ID不为空则直接启动拍卖程序
            if (TBPaiMaiId.Text.Trim() != "")
            {
                StartPaiMaiJob(TBPaiMaiId.Text, int.Parse(TBMaxPrice.Text));
                return;
            }

            //产品id不为空则，搜索拍卖页中是否有产品
            if (TBPaiMaiId.Text.Trim() == "" && TBProductId.Text.Trim() != "")
            {
                StarSearchAuctionByProductJob(TBProductId.Text.Trim(), int.Parse(TBMaxPrice.Text));
            }
        }

        private string GetPaiMaiJobName(string paimaiId)
        {
            return EnumType.JobType.SendPriceJob + "|" + paimaiId + "|" + Guid.NewGuid();
        }

        private void StartPaiMaiJob(string paimaiId, int myPrice)
        {
            //获取拍卖信息
            var paimaiItemDetail =
                GetPaiMaiDetailListFromWeb(new List<PaimaiItem> { new PaimaiItem { paimaiId = paimaiId } }).Single();
            if (paimaiItemDetail == null)
            {
                AddLog("\n" + "查询失败退出StartPaiMaiJob" + GetNow());
                return;
            }


            //设置拍卖结束时间为拍卖的开始时间
            var startTime = ConvertIntDateTime(paimaiItemDetail.endTime);
            //AddLog("\n" + "设定拍卖开始时间" + getNow());
            //判定登陆的帐号数目
            var i = 0;
            //每个帐号间隔80毫秒进行拍卖操作
            _accountsList.ForEach(account =>
            {
                //开启定时任务

                JobManager.AddJob(() =>
                {
                    //AddLog("\n" + "开始竞拍:" + paimaiId + " " + getNow());
                    StartPaimai(paimaiId, myPrice, account, Config.zeroTimeForPaimai * i + 80);
                }, t =>
                {
                    //t.ToRunNow().AndEvery(1).Seconds();
                    //提前多久开始出价
                    _scheduleAndPaimaiInfo.Add(t, paimaiItemDetail);
                    t.WithName(GetPaiMaiJobName(paimaiId))
                        .ToRunOnceAt(
                            startTime.AddMilliseconds(-Config.beforeBeginTime));
                });
                i++;
            });

            //拍卖结束后执行保存拍卖数据操作，并做统计
            JobManager.AddJob(() =>
            {
                var finishResult =
                    GetPaiMaiDetailListFromWeb(new List<PaimaiItem> { new PaimaiItem { paimaiId = paimaiId } }).Single();
                if (finishResult == null)
                {
                    AddLog("\n" + "查询失败退出:JobManager.AddJob" + "拍卖结束后执行保存拍卖数据操作");
                    return;
                }

                //AddLog("\n" + "开始竞拍:" + paimaiId + " " + getNow());
                //保存拍卖明细
                SavePaimaiDetail(finishResult);
                if (finishResult.auctionStatus == (int)EnumType.AuctionStatus.EndAuction)
                {
                    CalculateProductInfo(finishResult);
                }
            }, t => { t.ToRunOnceAt(startTime.AddMilliseconds(5)); });
        }

        /// <summary>
        ///     时间校准
        /// </summary>
        private void TimeFixTest(string paimaiId)
        {
            //开始测试时间校准
            richTextBox5.AppendText("\n开始校准:" + GetNow());
            var watch = new Stopwatch();
            watch.Start();
            var count = 1;
            while (true)
            {
                var result1 = GetPaiMaiDetailListFromWeb(new List<PaimaiItem> { new PaimaiItem { paimaiId = paimaiId } })
                    .Single();
                //richTextBox5.AppendText("\n" + count + " 次返回状态:" +  result1.auctionStatus + ":" + getNow());
                if (result1.auctionStatus == (int)EnumType.AuctionStatus.EndAuction)
                {
                    break;
                }

                count++;
            }
            watch.Stop();
            if (count < 2)
            {
                Config.beforeBeginTime = Config.beforeBeginTime + 100;
            }

            if (count > 2)
            {
                Config.beforeBeginTime = Config.beforeBeginTime - 100;
            }

            richTextBox5.AppendText("\n结束校准 响应次数:" + count + " 设置Span:" + Config.beforeBeginTime + " 耗时:" +
                                    watch.ElapsedMilliseconds + "  " + GetNow());
        }

        private EnumType.SendPriceStatus SendPrice(int price, string paimaiId, AccountInfo account)
        {
            var httphelper = new HttpHelper(true, account.virtualAccount);
            var httpItem = GetHttpItem();
            var t = new Random().Next(100000, 999999).ToString();
            httpItem.URL = "https://dbditem.jd.com/services/bid.action?t=" + t + "&paimaiId=" + paimaiId + "&price=" +
                           price + "&proxyFlag=0&bidSource=0";
            httpItem.Referer = "https://dbditem.jd.com/" + paimaiId;
            httpItem.Host = "dbditem.jd.com";
            httpItem.IsXRequestedWith = true;
            var result = httphelper.GetHtml(httpItem);


            //拍卖已结束
            if (result.Html.Contains("结束"))
            {
                return EnumType.SendPriceStatus.end;
            }

            if (result.Html.Contains("低于"))
            {
                return EnumType.SendPriceStatus.low;
            }

            if (result.Html.Contains("小于"))
            {
                return EnumType.SendPriceStatus.low;
            }

            if (result.Html.Contains("成功"))
            {
                return EnumType.SendPriceStatus.ok;
            }

            if (result.Html.Contains("京豆"))
            {
                return EnumType.SendPriceStatus.notEnoughJindou;
            }

            if (result.Html.Contains("频率过快"))
            {
                return EnumType.SendPriceStatus.tooFast;
            }

            if (result.Html.Contains("尚未登录"))
            {
                return EnumType.SendPriceStatus.notLogin;
            }

            return EnumType.SendPriceStatus.error;
        }

        /// <summary>
        ///     获取下一次出价的数值
        /// </summary>
        private int GetSendPrice(int currentPrice, string paimaiId)
        {
            var otherThreadPrice = (int)_paimaiPrice[paimaiId];
            //如果其他线程价格比当前价格高，则使用其他线程价格进行加价
            var basePrice = currentPrice > otherThreadPrice ? currentPrice : otherThreadPrice;
            //如果按百分比出价开关打开，则按百分比出价
            if (CHPercentPrice.Checked)
            {
                return basePrice + (int)(basePrice * PercentPriceBox.Value) / 100;
            }
            return basePrice + Config.everyTimePulsPrice;
        }

        /// <summary>
        ///     开始拍卖
        /// </summary>
        /// <param name="paimaiId"></param>
        /// <param name="maxPrice"></param>
        /// <param name="account"></param>
        /// <param name="startRemainTime"></param>
        private void StartPaimai(string paimaiId, double maxPrice, AccountInfo account, int startRemainTime)
        {
            if (maxPrice < 98 && maxPrice > 95)
            {
                maxPrice = 99;
            }
            if (!_paimaiPrice.ContainsKey(paimaiId))
            {
                lock (this)
                {
                    _paimaiPrice.Add(paimaiId, 0);
                }
            }

            var isPaimaiEnd = false;
            PaimaiItem paimaiItemDetail;
            //获取拍卖详情
            while (true)
            {
                paimaiItemDetail =
                    GetPaiMaiDetailListFromWeb(new List<PaimaiItem> { new PaimaiItem { paimaiId = paimaiId } }).Single();
                //到达指定的时间则开始发送拍卖请求
                if ((int)paimaiItemDetail.remainTime < startRemainTime)
                {
                    if ((int)paimaiItemDetail.remainTime == -1)
                    {
                        isPaimaiEnd = true;
                    }

                    break;
                }
            }

            var sendPrice = 0d;
            //其他线程的出价,如果其他线程出价高于当前价,则在其他线程出价基础上加价
            //var otherThreadPrice = paimaiPrice[paimaiId];
            sendPrice = GetSendPrice((int)paimaiItemDetail.currentPrice, paimaiId);
            if (sendPrice > maxPrice)
            {
                sendPrice = maxPrice;
            }

            if (paimaiItemDetail.currentPrice < maxPrice && !isPaimaiEnd)
            {
                if (sendPrice < 98 && sendPrice > 95)
                {
                    sendPrice = 99;
                }

                var result = SendPrice((int)sendPrice, paimaiId, account);
                if (result == EnumType.SendPriceStatus.ok)
                {

                    AddLog("\n出价成功:" + sendPrice + "  " + GetNow() + " " + account.virtualAccount);
                    _paimaiPrice[paimaiId] = (int)sendPrice;
                }

                if (result == EnumType.SendPriceStatus.low)
                {

                    AddLog("\n出价过低:" + sendPrice + "  " + GetNow() + " " + account.virtualAccount);
                }
                else if (result == EnumType.SendPriceStatus.end)
                {
                    AddLog("\n时拍结束出价:" + sendPrice + " " + GetNow() + " " + account.virtualAccount);
                }
                else if (result == EnumType.SendPriceStatus.notEnoughJindou)
                {
                    AddLog("\n京豆不足:" + GetNow() + " " + account.virtualAccount);
                }
                else if (result == EnumType.SendPriceStatus.tooFast)
                {
                    AddLog("\n出价太快 出价:" + sendPrice + " " + GetNow() + " " + account.virtualAccount);
                }
                else if (result == EnumType.SendPriceStatus.notLogin)
                {
                    AddLog("\n尚未登录:" + GetNow() + " " + account.virtualAccount);
                }
                else if (result == EnumType.SendPriceStatus.error)
                {
                    AddLog("\n异常退出:" + GetNow() + " " + account.virtualAccount);
                }
            }
            else if (isPaimaiEnd)
            {
                AddLog("\n未出价已结束:" + GetNow() + " " + account.virtualAccount);
            }
            else
            {
                AddLog("\n价格溢出:" + GetNow() + " " + account.virtualAccount);
            }

            //五秒后检查竞拍是否成功
            StartJobNow(() =>
            {
                lock (this)
                {
                    if (!_isPaimaiFinishChecked.ContainsKey(paimaiId))
                    {
                        var finishResult =
                            GetPaiMaiDetailListFromWeb(new List<PaimaiItem> { new PaimaiItem { paimaiId = paimaiId } })
                                .Single();
                        //如果结束后出价等于最终价格，恭喜你竞得商品
                        if (finishResult.auctionStatus == (int)EnumType.AuctionStatus.EndAuction &&
                            (int)_paimaiPrice[paimaiId] == (int)finishResult.currentPrice)
                        {
                            MessageBox.Show(account.virtualAccount + @"恭喜你获得竞拍");
                        }
                        else
                        {
                            AddLog("\n" + account.virtualAccount + "竞拍完成:" + paimaiId + " 出价:" +
                                   _paimaiPrice[paimaiId] + "最终价" + finishResult.currentPrice);
                        }

                        _isPaimaiFinishChecked.Add(paimaiId, true);
                    }
                }
            }, 5);
        }

        /// <summary>
        /// 获取拍卖的质量信息,包括商品外观，附件，包装等
        /// </summary>
        /// <param name="auctionId"></param>
        /// <returns></returns>
        private AuctionQuality GetAuctionQualityInfo(string auctionId)
        {
            var httphelper = new HttpHelper();
            var httpItem = GetHttpItem();
            httpItem.URL = "https://dbditem.jd.com/" + auctionId;
            var result = httphelper.GetHtml(httpItem);
            CQ dom = result.Html;
            var facade = dom["#auctionStatus2 .item_facade_info"].Html();
            var pack = dom["#auctionStatus2 .pack_facade_info"].Html();
            var accessory = dom["#auctionStatus2 .item_accessory_info"].Html();
            var use = dom["div h1 i.useIcon2"].Html();
            return new AuctionQuality(){ Use = use,Accessory = accessory,Facade = facade,Pakage = pack};
        }

        private string QueryAuctionIdForMyProduct(string productId,bool isGoodFacade = false,bool isGoodPakage = false,bool isNotUse = false)
        {
            var auction = SessionDic.CurrentAuctionList.Where(a => a.productId == productId);
            var paimaiItems = auction as PaimaiItem[] ?? auction.ToArray();
            foreach (var paimaiItem in paimaiItems)
            {
                //检查是否查询外观
                if (isGoodFacade || isGoodPakage || isNotUse)
                {
                    var quality = GetAuctionQualityInfo(paimaiItem.paimaiId);
                    if (isGoodFacade && quality.Facade != "商品外观良好")
                    {
                        continue;
                    }
                    if (isGoodPakage && quality.Facade != "外包装良好")
                    {
                        continue;
                    }
                    if (isNotUse && quality.Use != "未使用")
                    {
                        continue;
                    }
                    AddLog("\n" + "找到:" + productId + "的拍卖" + paimaiItems.First().paimaiId + " "+ quality.Use+" "+quality.Facade+" "+ quality.Facade + GetNow());
                    return paimaiItem.paimaiId;
                }
                
            }
            return "";
        }

        private void StarSearchAuctionByProductJob(string productId, int myPrice)
        {
            //开启定时任务
            JobManager.AddJob(() =>
            {
                AddLog("\n" + "开始查找:" + productId + "的拍卖" + GetNow());
                var auctionId = QueryAuctionIdForMyProduct(productId);
                if (auctionId.Length > 0)
                {
                    StartPaiMaiJob(auctionId, myPrice);
                    JobManager.RemoveJob(GetJobName(EnumType.JobType.SearchAuctionByProduct, productId));
                }
            }, t =>
            {
                //t.ToRunNow().AndEvery(1).Seconds();
                t.WithName(GetJobName(EnumType.JobType.SearchAuctionByProduct, productId)).ToRunNow().AndEvery(30).Seconds();
            });
        }

        /// <summary>
        ///     显示当前正在运行的拍卖job数据信息
        /// </summary>
        private void ShowAllPaimaiJob()
        {
            JobManager.AddJob(() =>
            {
                richTextBox2.Text = "";
                var allPaimaijob = JobManager.AllSchedules.Where(a => _scheduleAndPaimaiInfo.ContainsKey(a)).ToList();
                if (!allPaimaijob.Any())
                {
                    return;
                }

                allPaimaijob.ForEach(a =>
                {
                    if (_scheduleAndPaimaiInfo.ContainsKey(a))
                    {
                        var paimaiinfo = _scheduleAndPaimaiInfo[a];
                        richTextBox2.Text += @" " + paimaiinfo.paimaiId + @" " + a.NextRun.ToLongTimeString();
                    }
                });
            }, t => { t.ToRunEvery(2).Seconds(); });
        }

        #endregion
    }
}
