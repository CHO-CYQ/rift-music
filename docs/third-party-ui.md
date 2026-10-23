# UI artwork, typeface and icon attribution

## Application identity (0.5.3)

`Assets/RiftMusic.svg` is an original angular R/play monogram for 峡谷序曲, using the existing #321BDD / #F2F2F2 palette. It contains no official tournament or Riot logo. `scripts/render_brand_icon.py` renders this single polygon source to `RiftMusic.png` and the Windows `RiftMusic.ico` with 16/20/24/32/40/48/64/128/256 pixel sizes and transparent outer corners. The EXE native icon, WPF window/taskbar icon and tray icon share this ICO; the integrated title bar uses its matching PNG. Existing Fluent action icons and official event/category marks retain their separate attributions below.

## Artwork selection

Artwork follows the identity of a music track. A successfully decoded local `Track.CoverFile` has first priority, followed by an explicit track ID, then an exact verified theme and year. Theme matching applies only to skin and event tracks. A word in a title is not treated as proof of a theme.

Competition categories use their official transparent logos, without a black backing tile. Worlds opening tracks use verified annual Worlds theme artwork; BP uses the verified annual official promotional image, or the actual competition logo and year when an annual poster is unavailable. Worlds 2025 uses its official Xin Zhao image. BP across all competitions, imported tracks and unknown themes use neutral graphics. Known original publisher thumbnails are superseded only when their recorded hash matches; a user's replacement or custom cover keeps priority. Existing thumbnail files are retained on disk.

Annual skin images are checked against their year: Star Guardian 2017 uses Ahri, 2019 uses Rakan, and 2022 uses Kai'Sa. Specific champions get their own artwork, including Dark Star Cho'Gath, base Jhin, Swain, Teemo, Ambessa, Gun Goddess Miss Fortune and Battle Dove Seraphine. Noxus as a whole does not inherit Swain's base portrait.

## Riot Games artwork

The 73 packaged official images include Riot Games champion/skin artwork, transparent event logos and verified annual posters. Champion and skin illustrations come from Riot Games Data Dragon; names and skin numbers are checked against official `16.19.1` English metadata. The retained historical World Championship photograph comes from [Riot League Displays](https://displays.riotgames.com/en-us/); it is no longer the category default. The 2025 graphic comes from the [Worlds 2025 Primer](https://lolesports.com/en-US/news/worlds-2025-primer).

**League of Legends artwork © Riot Games. All rights reserved.** These files are not licensed under the MIT icon license or the font license. Their use does not imply Riot Games endorsement. Original downloaded JPEG bytes are retained without resampling or retouching. Art files left from the previous release are not used as generic category defaults.

Exact source URLs, official metadata, byte sizes and SHA-256 hashes are recorded in [artwork-sources.json](artwork-sources.json). Official resource documentation: [Riot Data Dragon](https://developer.riotgames.com/docs/lol#data-dragon).

## Worlds 2022 reference and open font alternatives

The additional unmodified `Worlds22Theme.jpg` comes from the original [Tendril project](https://tendril.studio/work/worlds2022/). It is a labelled 2022 visual-theme panel, separate from every track's existing cover mapping. Artwork rights remain with Riot Games and the respective creators.

Worlds 2022 used Manuka Bold and Fraktion. The application does not redistribute either commercial family. Version 0.5.2 uses Teko Bold for display headings, with Barlow Regular/Medium and IBM Plex Mono Regular; the previous Barlow Condensed Black file remains available. These fonts use SIL OFL 1.1, with all full licenses including `TEKO-OFL.txt` in ThirdParty. Teko is a static 700-weight instance generated from the official Google Fonts variable font. Pinned sources, processing and hashes are recorded in artwork-sources.json; original-font licensing and attribution are in worlds22-reference.md.

The display family uses `Teko` with Bold weight, verified against its actual embedded `Teko-Bold.ttf` URI. The body uses ordered WPF families for Latin Barlow/locally licensed Fraktion and Chinese Source Han Sans; actual GlyphRun checks verify both regular faces. Licensed original families available in Data/Fonts or system fonts are loaded locally and shown accurately in settings.

## Adobe Source Han Sans CN

The UI bundles the static Regular, Medium and Heavy OpenType faces of Source Han Sans CN (思源黑体 CN). They come from the official [Adobe Source Han Sans repository](https://github.com/adobe-fonts/source-han-sans/tree/release/SubsetOTF/CN), and do not require installing a system font. These are the unmodified official font files.

**Copyright 2014–2025 Adobe. Licensed under the SIL Open Font License 1.1.** The full license, including the reserved font name statement, is retained in `src/RiftMusic.App/Assets/Fonts/SOURCE-HAN-SANS-OFL.txt` and copied to the delivered `说明/ThirdParty` folder. Source URLs and digests are recorded in `artwork-sources.json`.

The embedded WPF family is `pack://application:,,,/RiftMusic;component/Assets/Fonts/#Source Han Sans CN`. Both Normal and Medium were verified with WPF `TryGetGlyphTypeface` to resolve to the respective embedded OTF file. Heavy resolves to the actual embedded SourceHanSansCN-Heavy.otf face; no synthesized face is claimed as an original Worlds font.

## Microsoft Fluent System Icons

The geometry resources in `src/RiftMusic.App/Assets/FluentIcons.xaml` come from the 24-pixel regular SVG icons in the official [Microsoft Fluent System Icons](https://github.com/microsoft/fluentui-system-icons) repository.

**Copyright (c) Microsoft Corporation. Licensed under the MIT License.** The complete downloaded license is retained in `src/RiftMusic.App/Assets/Art/FLUENT-ICONS-LICENSE.txt`; original SVG files remain in `src/RiftMusic.App/Assets/Art/FluentSources/`. URLs and SHA-256 hashes are recorded in `artwork-sources.json`. Original path data is preserved as WPF Geometry data; no runtime SVG parser is needed.

Exposed keys: `FluentPlay`, `FluentPause`, `FluentNext`, `FluentPrevious`, `FluentSearch`, `FluentMusic`, `FluentHeart`, `FluentTrophy`, `FluentSettings`, `FluentVolume`, `FluentDownload`. Neutral placeholders use these existing music, trophy and heart geometries, without claiming to be official music-release covers.

## Verification

`tests/RiftMusic.ArtworkTests` checks 73 official images with WPF, font/glyph availability, semantic year and hero mappings, all 148 installed track images, known-thumbnail substitution and user-cover priority. Version 0.5.2 adds use-specific year-label checks (271 total). `WorldsDesignTests` verifies actual embedded font URIs, mixed Latin/Chinese GlyphRuns and interrupted/completed motion (17 total). `LibraryTests` verifies the actual compact sidebar, transparent logo masks, three-column layout, animation and keyboard interaction (55 total). Missing or corrupt local artwork falls back to meaningful category artwork or a neutral image.

Assets are packaged locally; scrolling and automatic playback do not require an artwork download.


## 0.5.4 original AI atmosphere artwork

Two new 1774×887 PNGs were generated with the built-in imagegen tool, kept unedited and embedded locally. They are generic opening/BP atmosphere images, not official Riot event artwork. Full prompts, asset paths, dimensions and SHA-256 are in ai-atmosphere-v054.md and the separate generated_artwork manifest array. The 73 official images remain separately attributed. Current checks: Artwork 273/273, native library UI 68/68, Worlds font/motion 17/17.


## Alimama ShuHeiTi Chinese display font (0.5.5)

Official catalog: https://fonts.alibabagroup.com/ . The catalog lists AlimamaShuHeiTi-Bold and its downloadable official ZIP. The unmodified TTF is embedded in the application, with full legal notice and instructions included in ThirdParty. This is a documented Chinese display style alternative, not an original Worlds font. The legal notice permits free commercial/noncommercial use and forbids modifying the font or independently selling/licensing the font. Microsoft YaHei UI is resolved from Windows for readable Chinese body text; it is not redistributed. Hashes and URLs are in artwork-sources.json.
