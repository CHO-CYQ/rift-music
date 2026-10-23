# Worlds 2022 视觉参考与字体资源

核对日期：2026-10-06。以实际参与项目的设计公司和创作者为依据，没有使用社区字体猜测作为结论。

## 原版设计

[Tendril 的 Worlds 2022 项目](https://tendril.studio/work/worlds2022/)明确标注主字体 **Manuka Bold**，辅助字体 **Fraktion**；其[辅助字样规范图](https://tendril.studio/wp-content/uploads/2022/09/211172-1536x864.jpg)进一步列出 Fraktion Sans Bold、Fraktion Sans Regular、Fraktion Mono Bold。JetBrains Mono 不是该视觉规范中的字体。

[主字体规范图](https://tendril.studio/wp-content/uploads/2022/09/211171-1536x864.jpg)体现高大的压缩字形、紧密排字与强烈的黑白对比。[色彩规范图](https://tendril.studio/wp-content/uploads/2022/09/211169-1536x864.jpg)给出以下数值，而不是从视频或压缩图片估色：

| 规范名称 | 色值 | 本软件的使用方式 |
| --- | --- | --- |
| Sentinel White | `#F2F2F2` | 浅色底与大面积留白 |
| Mist Black | `#181818` | 字体、结构线与高对比色块 |
| Freljord Blue | `#321BDD` | 主操作、选中态与动效强调 |
| Shuriman Bronze | `#D6B598` | 少量专题与装饰强调 |

Tendril 以标志的承载形建立网格，并把「重新组合」作为二维图形共同的运动行为。[共同创意总监 Nidia Dias 的项目说明](https://nidiadias.com/works/riot-worlds-22)确认这是覆盖转播与赛事包装的统一体系。应用中的滑入、裁切揭示、网格与模块重组属于对这套语言的界面化演绎；具体时长和交互反馈是本软件设计，不宣称为官方规范数值。

## 交付字体与原版字体边界

**本版本没有分发 Manuka 或 Fraktion，没有使用试用字体伪装正式字体。** [Klim 的 Manuka 产品页](https://klim.co.nz/fonts/manuka/)和[授权总览](https://klim.co.nz/licences/)区分桌面、网页、App 与 Test 授权；Test 授权明确不允许嵌入。[Pangram Pangram 的 Fraktion 产品页](https://pangrampangram.com/products/fraktion)及[官方 FAQ](https://pangrampangram.com/pages/faq)同样说明软件内嵌需要 App 授权。

可直接分发的风格替代如下；既有OFL字体来自官方GitHub仓库的固定commit，保留完整SIL Open Font License 1.1。新增数黑体来自阿里官方，使用其独立法律声明。它们用于建立类似的字形角色，**不是官方 Worlds 字体**：

| 文件 | 内部 family | 字重 | 角色 |
| --- | --- | --- | --- |
| `Teko-Bold.ttf` | Teko | 700 | 0.5.2 默认窄体英文展示标题 |
| `BarlowCondensed-Black.ttf` | Barlow Condensed | 900 | 保留的旧版替代字库 |
| `Barlow-Regular.ttf` | Barlow | 400 | 英文常规信息 |
| `Barlow-Medium.ttf` | Barlow | 500 | 英文标签 |
| `IBMPlexMono-Regular.ttf` | IBM Plex Mono | 400 | 时长、年份和短数据标签 |
| `SourceHanSansCN-Heavy.otf` | Source Han Sans CN | 900 | 保留的旧版中文标题字库 |
| `AlimamaShuHeiTi-Bold.ttf` | Alimama ShuHeiTi | 700 | 0.5.5中文棱角标题风格替代，授权见文末 |

中文正文继续使用现有思源黑体 Regular / Medium。拉丁展示字体不能覆盖中文，不把中文依赖交给随机系统回退。Windows 字体名称记录与 typographic family 有差异：Barlow Black 的 Windows 传统 family 是 `Barlow Condensed Black`、typographic family 是 `Barlow Condensed`、宽度类为 Condensed（3）；Barlow Medium 的传统 family 是 `Barlow Medium`；思源 Heavy 的传统 family 是 `Source Han Sans CN Heavy`、typographic family 是 `Source Han Sans CN`。WPF 加载验证同时核对最终的 font URI，避免只显示回退字体却误以为字库已加载。

0.5.2 的原生 WPF 字形解析测试已确认，英文展示标题使用 `#Teko`＋`FontWeights.Bold`，解析到真正的 `Teko-Bold.ttf`。中文重点标题使用 `#Source Han Sans CN Heavy`＋`FontWeights.Black`，解析到真正的 Heavy 文件。混排正文通过实际 `DrawingVisual` 的 `GlyphRun` 检查确认：拉丁文字使用 `Barlow-Regular.ttf`，中文使用 `SourceHanSansCN-Regular.otf`。

Teko 来自 [Google Fonts 固定版本](https://github.com/google/fonts/tree/9710da1eacb3be272583c3224dcb70f9da6eadbb/ofl/teko)。官方变量字体通过 fonttools 固定为 wght=700，并更新内部名称；保留完整 `TEKO-OFL.txt`，未声明为Manuka原版。社区找到的Test Manuka“免费个人使用”下载没有提供允许随软件嵌入分发的依据，因此没有采用。

## 0.5.2 动效与紧凑分类

实际核对[2022官方专题](https://lpl.qq.com/es/worlds/2022/)的画面、CSS及JS；其网站使用GSAP、ScrollTrigger、Swiper和帧序列。通过yt-dlp取得用户提供的[52秒UI参考视频](https://www.bilibili.com/video/BV1FV4y157tQ/)可公开访问的SD版本，提取并查看分镜及26—28秒转场帧，确认窄体大字、蓝白块和横向裁切重组。[另一条设计视频](https://www.bilibili.com/video/BV15d4y1c7tT/)只核对了元数据，未将它记作已观看证据。参考视频不随软件打包。

通用年份面板使用三列、最多184px高、超出内部滚动，每个工作区只展开一类；折叠不会清除多选。展开240ms、收起180ms，箭头180ms，标题和主视觉裁切揭示320ms。切换、快速反向操作会释放旧动画时钟并恢复裁切；关闭动效或Windows关闭界面动画时立即完成。具体数值属于本软件设计。

五个官方赛事标志原文件已有透明通道，黑底来自旧界面底板；现在以透明图像作为单色遮罩，无需AI重绘。先锋赛2025橙色主视觉、2026紫色标志分别对应真实年份；同年多首共用赛事海报是合理的，新增年份标签避免跨年混淆。

Barlow 来自 [Google Fonts 官方仓库](https://github.com/google/fonts/tree/9710da1eacb3be272583c3224dcb70f9da6eadbb/ofl/barlow)，压缩体来自[同一固定版本](https://github.com/google/fonts/tree/9710da1eacb3be272583c3224dcb70f9da6eadbb/ofl/barlowcondensed)。IBM Plex Mono 来自 [IBM 官方仓库](https://github.com/IBM/plex/tree/763c36ef9117782905ae010056dfbe8fd2653a25/packages/plex-mono)。思源 Heavy 来自 [Adobe 官方仓库](https://github.com/adobe-fonts/source-han-sans/tree/a4f7cf94edfb9d7ffbdfc4841de276358bd7e0f2/SubsetOTF/CN)。各文件的 SHA-256、大小、内部名称与下载链接记录于 `artwork-sources.json`；fonttools 读取结果确认新字体 `fsType=0`，同时保留其 OFL 条款。

## 主题图的用途

`Assets/Art/Worlds22Theme.jpg` 是 Tendril 项目中公开展示的[Worlds 2022 赛事包装图](https://tendril.studio/wp-content/uploads/2022/09/a79920151716795.6317b3e37c421.jpg)，原文件未修改。用于本人的软件视觉主题参考，作品权利归 Riot Games / 对应创作者，视觉体系由 Tendril 制作；不属于软件图标或字体的 MIT / OFL 授权。`artwork-sources.json`保留原始地址、出处、用途与哈希。

这张图明确代表 **2022 视觉主题**。它不是其他年份主题曲的专辑封面，不改写既有歌曲与英雄/皮肤/赛事图片的映射，也不把软件描述成 Riot 官方产品。已有每首音乐的来源与封面优先级继续独立保留。


## 0.5.5 中文修正

腾讯2022专题CSS中的正文为Tahoma/微软雅黑，重点标题以PNG交付，没有确认可直接内嵌的中文标题原字库。本版用阿里妈妈官方数黑体Bold替换先前思源Heavy标题，明确标注风格替代；中文正文为系统微软雅黑UI，缺失时思源备用。新TTF来自阿里官方字库，不修改字库，许可证见ALIMAMA-SHUHEITI-LICENSE.txt。完整下载证据与实际WPF字体URI核对见player-repair-v055.md。采用Display排字和像素取整，提高小字可读性。
