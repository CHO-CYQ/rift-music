"""Write a human-readable inventory from the actual installed catalog."""
import json
import sys
from pathlib import Path

stage=Path(sys.argv[1]); data=json.loads((stage/'Data/settings.json').read_text(encoding='utf-8-sig'))
tracks=data['Tracks']; docs=stage/'说明';docs.mkdir(exist_ok=True)
categories={'worlds':'全球总决赛','skins':'英雄与皮肤','events':'活动与宇宙','seasons':'赛季与地区','custom':'本地导入'}
lines=['# 峡谷序曲 · 曲库与覆盖情况','',f'本版实际包含 **{len(tracks)} 首可播放音乐**。只统计有本地音频文件的条目。','',
'世界赛包含 S1–S15 共 15 个年份。S1 是早期登录主题的时期补编；S9 使用 Phoenix 伴奏剪辑；S13–S15 为明确标注的分离伴奏。并非所有条目都是当年游戏原始开场。','',
'原始游戏资源、官方主题剪辑、社区转载和分离伴奏分别标注。来源、原文件哈希、制作过程和检查记录见 `音乐来源.json` 与 `音源审查`。机器筛查不能代替人工听审，也不能保证分离伴奏没有细微处理痕迹。','']
for key,label in categories.items():
    group=sorted([t for t in tracks if t.get('Category')==key],key=lambda t:(t.get('Year',0) if key=='worlds' else t.get('Theme',''), t.get('Year',0),t['Name']))
    if not group:continue
    lines += [f'## {label} · {len(group)} 首','', '| 音乐 | 年份 | 版本 | 时长 |','|---|---:|---|---:|']
    for t in group:lines.append(f"| {t['Name'].replace('|','／')} | {t.get('Year') or '未确认'} | {t['Kind']} | {t['End']-t['Start']:.1f} 秒 |")
    lines.append('')
lines += ['## 用户点名主题的覆盖边界','',
'- 世界赛：覆盖 15 年，原版与补编的区别见上表和世界赛审查说明。S9 原始 spawn 在已查公开资源中仍未取得合格文件。',
'- 星之守护者、K/DA、斯维因、德玛西亚警官、提莫：保留客户端原始开场。',
'- 双城之战：2021 原曲通过连续波形比对确认；第二季收录斗士蔚的原始开场。',
'- 安蓓萨：已核验并收录客户端原始开场。',
'- 真实伤害：收录有来源记录的社区游戏音乐循环，标为社区剪辑；不是 GIANTS 含人声整曲。',
'- 幻灵：收录幻灵白鸽萨勒芬妮的完整被动器乐段，按客户端状态混合原始声部；不是原始开局音轨。',
'- 圣堂：本次覆盖萨恩·乌祖尔莫德凯撒与斗士蔚，不代表已收齐所有圣堂皮肤。',
'- 源计划：已收录源计划：升华烬的独立音乐，语音包中的旁白分支未混入。',
'- 奥德赛：官方登录主题的开场剪辑；不是曾经的游戏开场原轨。',
'- 死兆星：官方主题剪辑单独标注年份；2017 原始开场仍待合格来源，不把主题剪辑冒称原始开场。',
'- 赛季与地区：诺克萨斯、绽灵节、暮光试炼、德玛西亚已取得独立开场；旧 SN2022 / Hope24 保留客户端代号作为识别说明。',
'', '更多细目和未纳入候选的原因见音源审查说明。此版本是可继续扩充的拳头音乐库，不能宣称已收齐拳头全部游戏和全部作品。','',
'## 目录','', '`RiftMusic.exe` 是唯一启动入口；`Data` 保存音乐和你的设置；`说明` 保存清单、来源和验证材料。旧文件保留在上一级 `历史版本`，不必再运行旧版或修复脚本。','']
(docs/'曲库与覆盖情况.md').write_text('\n'.join(lines),encoding='utf-8')
print(f'Wrote inventory for {len(tracks)} real tracks')
