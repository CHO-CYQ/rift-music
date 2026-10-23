# BP 音源交付记录

核查日期：2026-10-04。目录服务于本地 BP 音乐应用；音乐用途按赛事与年份成对保存，不把主题曲、登场、赛前中场或比赛胜利音乐自动归入 BP。

`catalog.json` 有 82 个实际可解码的曲目记录，76 个默认启用。下载与审查文件总计 84 个：其中 1 个未识别的 LCK 广播提取文件不进入目录；Get Dropped 的2021/2023两份录音合并为1个曲目，保留两年的使用证据。76首启用文件合计387,444,045字节，约369.5MiB。

`catalog-enabled.json` 只含76首默认启用曲目，供本地应用打包。`excluded.json` 保留排除原因；不要把 research 目录全部音频直接打包。主目录的6首禁用曲目是 Full Beast Mode、On The Dot、Bang It Out、Something Lethal、Battle Me、The Main Event。

## 实际默认启用覆盖

下表是用途计数。同一首在不同年份或赛事复用时会重复计入用途，不能相加后称为不同歌曲数量。

| 年份 | Worlds/S | MSI | LCK | LPL |
|---|---:|---:|---:|---:|
| 2019 | 1 | 0 | 1 | 0 |
| 2020 | 0 | 0 | 1 | 0 |
| 2021 | 2 | 1 | 0 | 7 |
| 2022 | 8 | 3 | 0 | 9 |
| 2023 | 2 | 0 | 2 | 10 |
| 2024 | 1 | 4 | 2 | 11 |
| 2025 | 1 | 0 | 2 | 11 |
| 2026 | 0 | 1 | 1 | 8 |

赛事对应的不同音源数：Worlds13、MSI8、LCK5、LPL51；Bitten Bullet同时用于Worlds/MSI，四列相加比76多1。共有89个默认启用的赛事/年份配对。112个研究条目包含未取得音频或被排除的候选，不代表112首可用音乐。

## 来源与批量获取

主要批量源为孤情风影丶的逐年音乐合集，只获取明确标记 Banpick 的分P，保留分P URL、标题、上传者、时长和原始 SHA256。其他分P包含导播歌单、休息、解说席等，因此没有整合集无区别归类。

- [LPL2021夏季赛合集](https://www.bilibili.com/video/BV1VB4y1M7sj/)；[用途表](https://m.hupu.com/bbs/44753450.html)
- [LPL2022春季赛合集](https://www.bilibili.com/video/BV1yY41187eb/)
- [LPL2023合集](https://www.bilibili.com/video/BV1he4y1F7eq/)
- [LPL2024合集](https://www.bilibili.com/video/BV1si4y1W7x3/)
- [LPL2025合集](https://www.bilibili.com/video/BV1AmXxYHE5k/)；[曲目及作者表](https://www.bilibili.com/opus/1029452215781163012)
- [LPL2026合集](https://www.bilibili.com/video/BV13frWBxEW2/)；[曲目及作者表](https://www.bilibili.com/opus/1158960915883753472)
- [MSI2024合集](https://www.bilibili.com/video/BV12r421u7W1/)
- [MSI2025合集](https://www.bilibili.com/video/BV1wAgRzKEoK/)：取得的3首均排除，星号处理版与带词版本不能算已补齐。

歌曲原作者与赛事使用分开核实。可靠的歌曲身份来源包括 [Worlds2022官方制作音乐专辑](https://music.amazon.in/albums/B0CNKHJQC6)、[UPM Swagger Symphony中的Bitten Bullet](https://www.universalproductionmusic.com/de-de/discover/albums/14428/Swagger-Symphony)、[Compete To Win](https://www.universalproductionmusic.com/es-lae/discover/albums/36539/epic-orchestral-hybrid-vol-3)、[Start a Fight](https://upm-web3-prod-incap-01.universalproductionmusic.com/en-za/discover/albums/34702/sport-news-beds)、[Dimensions Collide](https://www.universalproductionmusic.com/pl-pl/discover/albums/34693/beyond-human-electronic-hybrid-trailers)、[Escalating Events](https://www.universalproductionmusic.com/en-row/discover/albums/8606/the-latest)、[Get Dropped](https://www.universalproductionmusic.com/en-us/discover/albums/14469/trap)。这些出版目录确认标题、作者和原版时长，不自动证明某场比赛曾使用它们。

Get Dropped的两份社区合集录音匹配同一UPM曲目CM122-1；抽取相同40秒区段，13ms对齐后相关系数0.984，已合并2021/2023用途。分析文件为`get-dropped-acoustic-compare.json`。

## LCK 的确切限制

取得的5首为 Escalating Events、Aerolite、Dimensions Collide、Compete To Win、Start a Fight。Aerolite是社区赛事编曲版，其他4首来源的时长匹配已识别出版目录；不将社区版本称为无损母带。Start a Fight的2026用途来自明确标注2025/2026 LCK CUP Game5的[音乐上传标题](https://www.youtube.com/watch?v=TBvJNHV3vHQ)，仍为社区证据。

[Life Is C Between B and D官方SoundCloud](https://soundcloud.com/lck_official/life-is-c-between-b-and-d-2023)的描述明确为LCK Ban Phase；[官方YouTube](https://www.youtube.com/watch?v=2va8q8706-M)有复制限制，保留链接。Prelude To Battle - The Trilogy、Crossroads同样仅保留官方播放链接，没有转而下载它们的社区转载版本。

LCK2021/2022仅找到质量或解说残留有疑点的广播提取版，未进入可用库。MSI2023 On The Dot上传标题承认去解说处理，已排除。MSI2025三首仍需要可确认的原版纯音乐来源。Worlds2020本次未成功取得已验证音源；2026 Worlds截至核查日期未核实到实际BP发行或使用。以上缺口没有用其他年份或赛事主题曲填充。

## 检查与质量含义

每个进入目录的MP3都经ffprobe实际时长读取、ffmpeg整首严格解码，检查File存在、SHA256一致、Start=0、End=Duration、淡入淡出=0及至少一个用途配对。`delivery-verification.json`没有结构/文件/哈希/区间/样本ASR错误。

全部84个实际音频都抽取首段、中段、末段各12秒，使用本地Whisper tiny English做36秒ASR。默认启用76首的样本没有检出词句；这不等于整首人工听检，也不能排除未采样区段的人声。Full Beast Mode、Bang It Out、Something Lethal、Battle Me有词句或可识别人声诊断；On The Dot与The Main Event依据来源性质排除。详细记录在`audio-qc.json`和`qc/`。

来源容器中的部分Opus文件有头部解析警告；最终供应用使用的MP3整首严格解码通过。MP3为192kbps兼容转码，不会提高原始音源质量。原始文件、元数据和哈希保留。社区延长循环版保留实际完整时长并标明类型；没有凭猜测截断循环，也没有把前奏淡出。

37个目录记录的原作者仍待核，以“原作者待核”显示。社区使用表与上传者用途标注一般为medium confidence；官方明确BP发行才标为high。信心、来源差异和不确定性保留在`sources.json`，应用的Track结构不会完整保存这些额外审查字段，交付时应同时保留本文件和来源清单。

所有获取均为公开页面及公开音乐资源，没有读取登录凭据、购买、绕过鉴权或DRM。本记录没有声称这些文件获得公开再分发授权。
