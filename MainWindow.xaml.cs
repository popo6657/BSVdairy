using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BitcoinSVCryptor;
using BsvSimpleLibrary;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace diary
{
    /// <summary>
    /// 基于BSV区块链的加密日记本主窗口类
    /// 支持日记的加密保存、区块链存储、查询和展示功能
    /// </summary>
    
    public partial class MainWindow : Window
    {
        // 私钥：用于加密日记内容和签署BSV交易
        private const string PRIVATE_KEY = "你的私钥";
        // 网络类型：test表示使用测试网络，mainnet表示主网
        private const string NETWORK = "test";

        /// <summary>
        /// 日记记录的数据模型类
        /// 存储单条日记的所有相关信息
        /// </summary>
        public class DiaryRecord
        {
            public string TxId { get; set; } = "";          // 区块链交易ID
            public string Timestamp { get; set; } = "";     // 日记时间戳
            public string DecryptedContent { get; set; } = "";  // 解密后的日记内容
            public string Preview => (DecryptedContent?.Length > 30 ?
                DecryptedContent.Substring(0, 30) + "..." : DecryptedContent) ?? "";
        }

        /// <summary>
        /// 构造函数：初始化界面组件
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;    // 窗口加载完成时触发初始化
        }

        /// <summary>
        /// 窗口加载完成事件处理
        /// 异步初始化钱包和加载历史日记
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => InitWallet());
        }

        /// <summary>
        /// 初始化钱包：生成地址并加载历史日记
        /// </summary>
        private void InitWallet()
        {
            try
            {
                // 从私钥获取对应的钱包地址
                string address = GetAddress();

                // 在UI线程更新地址显示
                Dispatcher.Invoke(() =>
                {
                    txtWalletInfo.Text = $"🔑 地址: {address}";
                });
                
                // 加载所有历史日记
                LoadAllDiaries();
            }
            catch (Exception ex)
            {
                // 显示错误信息
                Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = $"❌ 初始化失败: {ex.Message}";
                });
            }
        }

        /// <summary>
        /// 从私钥获取比特币地址
        /// </summary>
        /// <returns>钱包地址字符串</returns>
        private string GetAddress()
        {
            var secret = new BitcoinSecret(PRIVATE_KEY, Network.TestNet);
            return secret.GetAddress(ScriptPubKeyType.Legacy).ToString();
        }

        /// <summary>
        /// 保存日记按钮点击事件
        /// 加密日记内容并写入BSV区块链
        /// </summary>
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string content = txtDiaryInput.Text.Trim();
            if (string.IsNullOrEmpty(content))
            {
                MessageBox.Show("请输入日记内容！", "提示");
                return;
            }

            try
            {
                txtStatus.Text = "🔐 正在加密...";

                // 在后台线程执行加密和区块链写入操作
                await Task.Run(() =>
                {
                    // 使用AES加密日记内容，私钥作为加密密钥
                    byte[] encrypted = AES_class.AesEncrypt(content, PRIVATE_KEY);
                    string encryptedBase64 = Convert.ToBase64String(encrypted);
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    
                    // 格式化OP_RETURN数据：DIARY|时间戳|加密内容
                    string opreturnData = $"DIARY|{timestamp}|{encryptedBase64}";

                    Dispatcher.Invoke(() => txtStatus.Text = "⛓️ 正在写入区块链...");

                    // 发送BSV交易，包含OP_RETURN输出
                    var result = bsvTransaction_class.send(
                        PRIVATE_KEY,                   // 私钥，用于签名交易
                        0,                             // 交易金额（0表示只发送OP_RETURN）
                        NETWORK,                       // 网络类型（test/mainnet）
                        destAddressStr: null,          // 目标地址（null表示不发送BSV）
                        changeBackAddressStr: null,    // 找零地址（自动管理）
                        opreturnData: opreturnData,    // OP_RETURN数据
                        feeSatPerByte: 1.0             // 每字节手续费（聪）
                    );

                    // 在UI线程更新界面
                    Dispatcher.Invoke(() =>
                    {
                        txtDiaryInput.Clear();

                        // 检查是否有错误
                        if (result.ContainsKey("Error"))
                        {
                            txtStatus.Text = $"❌ 失败: {result["Error"]}";
                            MessageBox.Show($"保存失败: {result["Error"]}", "错误");
                            return;
                        }

                        // 获取交易ID并显示成功对话框
                        string txid = result.ContainsKey("send info") ? result["send info"] : "";
                        txtStatus.Text = $"✅ 保存成功！";
                        ShowTxIdDialog(txid);
                    });
                });

                // 等待3秒后重新加载日记列表（确保区块链已同步）
                await Task.Delay(3000);
                await Task.Run(() => LoadAllDiaries());
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"❌ 保存失败: {ex.Message}";
                MessageBox.Show($"保存失败: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 显示交易ID对话框
        /// 提供复制交易ID和关闭对话框的功能
        /// </summary>
        private void ShowTxIdDialog(string txid)
        {
            // 创建自定义对话框窗口
            var dialog = new Window
            {
                Title = "日记保存成功！",
                Width = 520,
                Height = 260,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new LinearGradientBrush(
                    Color.FromRgb(255, 248, 28),
                    Color.FromRgb(255, 241, 160),
                    new Point(0, 0), new Point(0, 1))
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            // 添加标题
            panel.Children.Add(new TextBlock
            {
                Text = "🎉 日记已写入BSV区块链！",
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            // 添加交易ID标签
            panel.Children.Add(new TextBlock
            {
                Text = "交易ID (TXID):",
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 5)
            });

            // 添加交易ID文本框（只读）
            var txidBox = new TextBox
            {
                Text = txid,
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(Color.FromRgb(255, 249, 196)),
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(txidBox);
            
            // 按钮面板
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 复制按钮
            var copyBtn = new Button
            {
                Content = "📋 复制TXID",
                Width = 130,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(78, 205, 196)),
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Comic Sans MS"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            copyBtn.Click += (s, ev) =>
            {
                Clipboard.SetText(txid);
                copyBtn.Content = "✅ 已复制！";
            };
            btnPanel.Children.Add(copyBtn);

            // 关闭按钮
            var closeBtn = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(255, 107, 107)),
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Comic Sans MS"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (s, ev) => dialog.Close();
            btnPanel.Children.Add(closeBtn);

            panel.Children.Add(btnPanel);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        /// <summary>
        /// 加载所有日记按钮点击事件
        /// </summary>
        private void BtnLoadAll_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => LoadAllDiaries());
        }

        /// <summary>
        /// 从区块链加载所有历史日记
        /// 解析交易中的OP_RETURN数据并解密
        /// </summary>
        private void LoadAllDiaries()
        {
            try
            {
                Dispatcher.Invoke(() => txtStatus.Text = "🔍 正在加载日记...");

                string address = GetAddress();

                // 获取该地址的所有交易历史
                var txs = RestApi_class.getAddressHistory(
                    bsvConfiguration_class.RestApiUri, NETWORK, address);

                var records = new List<DiaryRecord>();

                if (txs != null && txs.Length > 0)
                {
                    // 过滤出已确认的交易，按区块高度降序排序，取最新的50条
                    var sortedTxs = txs
                        .Where(t => t.Height > 0)
                        .OrderByDescending(t => t.Height)
                        .Take(50)
                        .ToList();

                    foreach (var tx in sortedTxs)
                    {
                        try
                        {
                            // 获取原始交易数据
                            string rawTx = RestApi_class.getRawTransaction(
                                bsvConfiguration_class.RestApiUri, NETWORK, tx.TxHash);

                            if (string.IsNullOrEmpty(rawTx)) continue;

                            // 检查是否包含DIARY标记（十六进制格式）
                            string diaryMarker = "DIARY|";
                            byte[] markerBytes = Encoding.UTF8.GetBytes(diaryMarker);
                            string markerHex = Encoders.Hex.EncodeData(markerBytes);

                            if (!rawTx.Contains(markerHex)) continue;

                            // 获取详细的交易信息
                            var txDetail = RestApi_class.getTransaction(
                                bsvConfiguration_class.RestApiUri, NETWORK, tx.TxHash);

                            if (txDetail == null) continue;

                            // 获取OP_RETURN数据
                            string opreturnData = RestApi_class.getOpReturnData(txDetail, Encoding.UTF8);

                            // 如果上面的方法获取失败，手动解析输出中的nulldata
                            if (string.IsNullOrEmpty(opreturnData) && txDetail.Outputs != null)
                            {
                                foreach (var output in txDetail.Outputs)
                                {
                                    if (output.ScriptPubKey != null &&
                                        output.ScriptPubKey.Type == "nulldata")
                                    {
                                        string hex = output.ScriptPubKey.Hex;
                                        if (!string.IsNullOrEmpty(hex) && hex.Length > 4)
                                        {
                                            // 去除OP_RETURN操作码前缀
                                            string dataHex = hex.StartsWith("006a") ? hex.Substring(4) : hex.Substring(2);
                                            byte[] dataBytes = Encoders.Hex.DecodeData(dataHex);
                                            opreturnData = Encoding.UTF8.GetString(dataBytes);
                                        }
                                        break;
                                    }
                                }
                            }

                            // 验证并解析DIARY格式数据
                            if (!string.IsNullOrEmpty(opreturnData) && opreturnData.StartsWith("DIARY|"))
                            {
                                string[] parts = opreturnData.Split('|');
                                if (parts.Length >= 3)
                                {
                                    string timestamp = parts[1];
                                    string encryptedBase64 = parts[2];
                                    byte[] encrypted = Convert.FromBase64String(encryptedBase64);

                                    // 使用AES解密日记内容
                                    string decrypted = AES_class.AesDecrypt(encrypted, PRIVATE_KEY);

                                    // 添加到记录列表
                                    records.Add(new DiaryRecord
                                    {
                                        TxId = tx.TxHash,
                                        Timestamp = timestamp,
                                        DecryptedContent = decrypted
                                    });
                                }
                            }
                        }
                        catch { continue; }   // 跳过解析失败的交易
                    }
                }

                // 在UI线程更新日记列表
                Dispatcher.Invoke(() =>
                {
                    listDiaries.ItemsSource = null;
                    listDiaries.ItemsSource = records;
                    txtStatus.Text = $"✨ 加载完成，共 {records.Count} 条日记";

                    if (records.Count > 0)
                    {
                        listDiaries.SelectedIndex = 0;
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => txtStatus.Text = $"❌ 加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 日记列表选择改变事件
        /// 显示选中日记的详细内容
        /// </summary>
        private void ListDiaries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listDiaries.SelectedItem is DiaryRecord record)
            {
                // 在显示区域展示日记内容
                txtDiaryDisplay.Text = $"⏰ 时间: {record.Timestamp}\n\n📖 日记内容:\n{record.DecryptedContent}";
                
                // 同时将内容加载到编辑框，方便编辑
                txtDiaryInput.Text = record.DecryptedContent;
            }
            else
            {
                txtDiaryDisplay.Text = "请从右侧列表选择一条日记";
            }
        }
    }
}