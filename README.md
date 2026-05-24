# BSV加密日记本

基于 BSV 区块链的加密日记本桌面应用。用户编写的日记内容经过 AES 加密后，通过 OP_RETURN 写入 BSV 测试网区块链，实现不可篡改、可追溯的私密日记存储。

## 功能特性

- **AES 加密存储** — 日记内容使用私钥进行 AES 加密，确保链上数据不可被第三方直接读取
- **区块链持久化** — 加密后的日记通过 OP_RETURN 交易写入 BSV 区块链，永久保存、不可篡改
- **历史日记加载** — 自动从链上检索并解密所有历史日记，支持按时间排序和列表预览
- **交易信息查看** — 保存成功后弹窗展示交易 ID（TXID），支持一键复制
- **手绘风格 UI** — 采用 Comic Sans 字体、明快配色和倾斜按钮，视觉风格活泼

## 技术栈

| 层面 | 技术 |
|------|------|
| 框架 | .NET 7.0 / WPF |
| 区块链 | BSV Testnet |
| 加密 | AES（BitcoinSVCryptor） |
| 交易构建 | BsvSimpleLibrary |
| 密钥管理 | NBitcoin / NBitcoin.Altcoins |
| 加密底层 | BouncyCastle |
| JSON 解析 | Newtonsoft.Json |

## 项目结构

`
BSVdairy/
├── MainWindow.xaml          # 主窗口 XAML 布局
├── MainWindow.xaml.cs       # 主窗口业务逻辑
├── App.xaml                 # WPF 应用入口配置
├── App.xaml.cs              # App 启动代码
├── dairy1.csproj            # 项目文件及 NuGet 依赖
└── dairy1.sln               # 解决方案文件
`

## 核心流程

`
写日记
  └─ 用户输入文本
       └─ AES 加密（私钥为密钥）
            └─ 编码为 Base64
                 └─ 拼接 OP_RETURN 数据: DIARY|时间戳|密文
                      └─ 发送 BSV 交易上链

加载日记
  └─ 通过钱包地址查询链上交易历史
       └─ 解析每笔交易的 OP_RETURN 输出
            └─ 筛选 DIARY| 前缀的数据
                 └─ AES 解密还原日记内容
                      └─ 展示在界面上
`

## 快速开始

### 环境要求

- Windows 10/11
- .NET 7.0 SDK
- Visual Studio 2022（推荐）或 dotnet CLI

### 配置私钥

打开 MainWindow.xaml.cs，将常量替换为你自己的 BSV 测试网私钥：

`csharp
private const string PRIVATE_KEY = "你的私钥";
private const string NETWORK = "test";  // "test" 为测试网，"mainnet" 为主网
`

### 编译运行

`ash
# 使用 CLI
dotnet build
dotnet run

# 或在 Visual Studio 中打开 dairy1.sln 直接运行
`

## 数据格式

日记在链上以 OP_RETURN 形式存储，格式如下：

`
DIARY|2026-05-24 14:30:00|Base64EncodedAESCiphertext
`

- DIARY — 固定标记，用于识别日记类交易
- 时间戳 — yyyy-MM-dd HH:mm:ss 格式
- 密文 — AES 加密后经 Base64 编码的日记内容

## 注意事项

- 默认配置使用 **BSV 测试网**，如需切换主网请将 NETWORK 改为 "mainnet"
- 私钥直接硬编码在源码中，仅适用于课程演示场景；生产环境应使用安全的密钥管理方案
- 链上数据公开可读，日记隐私依赖 AES 加密保护；请妥善保管私钥
- 加载日记时默认取最近 50 条已确认交易
