# 《CPU 100%》美术素材清单

把素材按下面的文件名放进对应文件夹即可。当前版本全部用程序生成的占位图，
下一版我会把所有占位图替换成你放入的正式素材。

**导入建议**：全部 PNG（透明背景），导入类型 Sprite (2D and UI)。
桌面图标类素材建议正方形（128×128 或 256×256 均可）——替换时会把
Pixels Per Unit 设为图片边长，让图标正好等于 1 个世界单位，和现在的占位尺寸一致。

## Wallpaper/ — 桌面壁纸

| 文件名 | 内容 | 建议尺寸 |
|---|---|---|
| `wallpaper.png` | 电脑桌面壁纸（整个游戏背景，会铺满 16:9 画面） | 1920×1080 |

## Icons/ — 桌面图标（游戏里的所有平台）

每个图标就是一个可站立的平台，画的时候记得它们要"像桌面图标"（图标主体清晰、
方形轮廓为主）。名字文字由游戏内程序显示，素材里**不用**画文件名。

| 文件名 | 内容 | 对应场景物体 |
|---|---|---|
| `icon_folder.png` | 文件夹（黄色经典款，玩家出生平台 "Documents"） | StartFolder |
| `icon_text_file.png` | 文本文件 .txt（白纸+文字线条） | TextFilePlatform |
| `icon_image_file.png` | 图片文件 .png（白纸+风景缩略图） | ImageFilePlatform |
| `icon_browser.png` | 浏览器软件 "Browser.exe"（可拖拽安装） | BrowserSoftware |
| `icon_paper_plane.png` | 纸飞机软件 "Paper Plane.exe"（可拖拽安装） | PaperPlaneSoftware |
| `icon_shield.png` | 防护软件 "Shield.exe"（可拖拽安装） | ShieldSoftware |
| `icon_virus.png` | 病毒文件 "virus.exe"（危险感，红色系） | VirusFile |
| `icon_system_file.png` | 系统文件 "system32.dll"（齿轮/系统感） | SystemFile |
| `icon_recycle_bin.png` | 回收站 | RecycleBin |
| `icon_accelerator.png` | 加速器 "System Booster.lnk"（终点，胜利目标，火箭/上箭头） | AcceleratorShortcut |
| `icon_web_shortcut.png` | 临时网页快捷方式 "New Tab.url"（浏览器技能生成的临时平台） | 运行时生成 |
| `shortcut_arrow.png` | 快捷方式左下角的小箭头角标 | 叠加在快捷方式图标上 |

## Player/ — 玩家角色（杀毒软件桌宠）

| 文件名 | 内容 | 建议尺寸 |
|---|---|---|
| `player_idle.png` | 待机（第一版单帧即可） | ~128×160（宽:高 ≈ 0.7:0.9） |
| `player_run_01..04.png` | 跑动序列帧（可选，后续接 Animator） | 同上 |
| `player_jump.png` | 跳跃帧（可选） | 同上 |
| `player_fall.png` | 下落帧（可选） | 同上 |

## Cursor/ — 虚拟鼠标

| 文件名 | 内容 | 建议尺寸 |
|---|---|---|
| `cursor.png` | 自定义鼠标指针（热点在左上角） | 64×64 |

## UI/Taskbar/ — 任务栏

| 文件名 | 内容 |
|---|---|
| `taskbar_bg.png` | 任务栏底条背景（可做 9-slice） |
| `slot_frame.png` | 软件槽位边框 |
| `slot_selected.png` | 槽位选中高亮 |
| `slot_running.png` | 正在运行指示（小灯条/下划线） |
| `close_button.png` | 关闭按钮 × |
| `start_button.png` | 开始菜单按钮图标 |

## UI/CPUWindow/ — 左上角 CPU 窗口（仿任务管理器）

| 文件名 | 内容 |
|---|---|
| `cpu_window_frame.png` | 小浮窗边框（带标题栏，可 9-slice） |
| `cpu_bar_fill.png` | CPU 进度条填充 |
| `thermometer.png` | 温度计占位图 |

## UI/Overlays/ — 全屏遮罩

| 文件名 | 内容 |
|---|---|
| `noise.png` | 雪花/噪点纹理（崩坏区域 + 高 CPU 故障遮罩，可平铺，带透明度） |
| `warning_icon.png` | 警告标志 ⚠（输入干扰预警用，可选） |

## UI/Screens/ — 结算画面

| 文件名 | 内容 |
|---|---|
| `bluescreen_bg.png` | 蓝屏失败画面背景（可选——现在是纯色蓝 + 文字，也可以只出 :( 表情素材） |
| `victory_bg.png` | "System Repaired" 胜利画面背景（可选） |

## 优先级建议（时间不够先画这些）

1. 10 个桌面图标 + 快捷方式小箭头（游戏画面主体）
2. 玩家角色 idle 单帧
3. 壁纸
4. 噪点纹理、光标
5. 任务栏 / CPU 窗口 /结算画面（现在的程序占位 UI 其实已经能看）
