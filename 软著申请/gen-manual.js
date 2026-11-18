// 软著申请材料：用户手册（操作说明书）生成器
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, Header, Footer, AlignmentType,
  PageNumber, PageBreak, Table, TableRow, TableCell, WidthType, BorderStyle,
  ShadingType, LevelFormat, HeadingLevel
} = require("docx");

const OUT = path.join(__dirname, "用户手册-音乐定时管家软件V1.0.0.docx");
const SOFT_NAME = "音乐定时管家软件";
const VERSION = "V1.0.0";
const OWNER = "深圳安信联创科技有限公司";

const zhFont = { ascii: "Times New Roman", eastAsia: "宋体", hAnsi: "Times New Roman" };
const zhHei = { ascii: "SimHei", eastAsia: "黑体", hAnsi: "SimHei" };

const P = (text, opts = {}) => new Paragraph({
  alignment: opts.align,
  spacing: { before: opts.before ?? 60, after: opts.after ?? 60, line: 300 },
  children: [new TextRun({
    text, font: opts.bold ? zhHei : zhFont,
    size: opts.size ?? 21, bold: opts.bold
  })]
});

const H1 = (text) => new Paragraph({
  heading: HeadingLevel.HEADING_1,
  spacing: { before: 280, after: 160 },
  children: [new TextRun({ text, font: zhHei, size: 30, bold: true })]
});
const H2 = (text) => new Paragraph({
  heading: HeadingLevel.HEADING_2,
  spacing: { before: 200, after: 120 },
  children: [new TextRun({ text, font: zhHei, size: 24, bold: true })]
});

const border = { style: BorderStyle.SINGLE, size: 2, color: "999999" };
const borders = { top: border, bottom: border, left: border, right: border };
const CELL_W = [1800, 7260];
function cell(text, w, head = false) {
  return new TableCell({
    borders, width: { size: w, type: WidthType.DXA },
    shading: head ? { fill: "E8EEF7", type: ShadingType.CLEAR } : undefined,
    margins: { top: 60, bottom: 60, left: 100, right: 100 },
    children: [P(text, { size: 19, before: 20, after: 20, bold: head })]
  });
}
function table(rows) {
  return new Table({
    width: { size: 9060, type: WidthType.DXA },
    columnWidths: CELL_W,
    rows: rows.map((r, i) => new TableRow({
      children: [cell(r[0], CELL_W[0], i === 0), cell(r[1], CELL_W[1], i === 0)]
    }))
  });
}

const li = (text) => new Paragraph({
  numbering: { reference: "num", level: 0 },
  spacing: { before: 40, after: 40, line: 300 },
  children: [new TextRun({ text, font: zhFont, size: 21 })]
});

// ---------- 封面 ----------
const cover = [
  new Paragraph({ spacing: { before: 2800 } }),
  P(SOFT_NAME, { align: AlignmentType.CENTER, size: 72, bold: true, before: 200, after: 200 }),
  P(`（${VERSION}）`, { align: AlignmentType.CENTER, size: 40, before: 100, after: 400 }),
  P("用 户 手 册", { align: AlignmentType.CENTER, size: 52, bold: true, before: 400, after: 2400 }),
  P(`著作权人：${OWNER}`, { align: AlignmentType.CENTER, size: 24, before: 2400 }),
  new Paragraph({ children: [new PageBreak()] })
];

// ---------- 正文 ----------
const body = [];

body.push(H1("一、软件概述"));
body.push(P(`${SOFT_NAME}（${VERSION}）是一款运行于 Windows 平台的桌面音乐定时播放软件。软件支持 MP3、AAC、M4A、FLAC、WAV、WMA 等常见音频格式，用户可创建多个定时播放任务，软件按任务设定在精确到秒的时间点自动开始播放音乐，并在任务结束时间自动停止，全过程无需人工干预。`));
body.push(P("本软件采用内置的自研秒级调度引擎（500 毫秒周期扫描），不依赖 Windows 操作系统自带的计划任务程序；软件支持最小化到系统托盘常驻运行，可在后台长期稳定工作。主要功能如下："));
body.push(table([
  ["功能模块", "功能说明"],
  ["定时任务管理", "新建、编辑、复制、删除定时播放任务；每个任务可包含单曲或多首歌曲列表"],
  ["重复规则", "一次性（指定日期）、每天、每周任意星期组合"],
  ["播放控制", "播放、暂停、上一首、下一首、停止、进度拖动、音量调节、10 段图形均衡器"],
  ["播放模式", "顺序播放、循环播放"],
  ["冲突检测", "保存任务时自动展开未来 7 天的时间区间，与其他任务重叠时拒绝保存并提示冲突详情"],
  ["任务导入导出", "任务列表可导出为 JSON 文件备份，也可从文件导入并自动跳过冲突任务"],
  ["播放列表库", "可将任务歌曲另存为命名播放列表，供其他任务快速插入复用"],
  ["任务专属均衡器", "每个任务可指定均衡器预设（如低音增强、人声、电子等），播放该任务时自动切换"],
  ["系统托盘", "关闭主窗口自动最小化到托盘；托盘菜单支持打开主界面、停止播放、关于、退出"],
  ["开机自启", "一键设置开机自动启动（写入当前用户注册表，无需管理员权限）"],
  ["数据保护", "任务数据采用原子写入保存；单实例保护防止重复启动"],
]));

body.push(new Paragraph({ children: [new PageBreak()] }));
body.push(H1("二、运行环境"));
body.push(table([
  ["项目", "要求"],
  ["操作系统", "Windows 10 / Windows 11（64 位）"],
  ["硬件要求", "普通个人电脑即可，无特殊要求"],
  ["运行时依赖", "无（自包含发布，已内置 .NET 运行时，无需额外安装）"],
  ["权限要求", "普通用户权限即可，无需管理员"],
  ["音频设备", "声卡 / 扬声器或耳机"],
]));

body.push(H1("三、安装与卸载"));
body.push(H2("3.1 安装版"));
body.push(li("双击安装程序 MusicScheduler-Setup-v1.0.0.exe；"));
body.push(li("按安装向导提示选择安装目录（默认安装到当前用户目录，无需管理员权限）；"));
body.push(li("完成安装后，可从开始菜单或桌面快捷方式启动软件；"));
body.push(li("如需开机自动启动，可在软件「设置」窗口中一键开启。"));
body.push(H2("3.2 绿色版"));
body.push(li("将软件目录解压到任意位置（若从网络下载的压缩包，建议先对主程序执行解除锁定操作）；"));
body.push(li("双击目录中的 MusicScheduler.exe 直接运行，无需安装。"));
body.push(H2("3.3 卸载"));
body.push(li("安装版：通过 Windows「设置 → 应用」中卸载，或运行安装目录中的卸载程序；"));
body.push(li("绿色版：直接删除软件目录即可。"));
body.push(P("卸载后用户数据（任务、设置）保留在用户数据目录中，如需彻底清除可一并删除（见第七节）。"));

body.push(new Paragraph({ children: [new PageBreak()] }));
body.push(H1("四、功能操作详解"));
body.push(H2("4.1 主界面"));
body.push(P("软件启动后显示主窗口。窗口左侧为播放器面板，显示当前播放状态、歌曲信息、播放进度条、播放控制按钮（上一首、播放/暂停、下一首、停止）、音量滑杆和 10 段图形均衡器；窗口右侧为任务列表区，以卡片形式展示所有定时任务，每张卡片显示任务名称、执行时间、重复规则、播放模式、曲目数量与状态；窗口顶部提供「新建任务」「导入」「导出」「设置」「关于」等功能入口，并显示当前时间与下一次任务的倒计时提示。"));

body.push(H2("4.2 新建任务"));
body.push(li("点击主界面「+ 新建任务」按钮，弹出任务编辑窗口；"));
body.push(li("填写任务名称；"));
body.push(li("设置开始时间与结束时间（精确到秒，格式 HH:MM:SS），结束时间必须晚于开始时间；"));
body.push(li("选择重复规则：一次性（需指定日期）/ 每天 / 每周（勾选星期一至星期日的任意组合）；"));
body.push(li("添加播放曲目：点击「添加文件」选择一个或多个音频文件，支持上下移动调整顺序；也可从播放列表库一键插入；"));
body.push(li("选择播放模式：顺序播放（按列表播完即止）或循环播放（在任务时间范围内循环）；"));
body.push(li("（可选）为该任务指定均衡器预设，未指定时跟随全局均衡器设置；"));
body.push(li("点击「保存」。若与其他任务的时间区间重叠，软件将拒绝保存并在窗口中以红色文字列出冲突详情，用户可调整时间后重新保存。"));

body.push(H2("4.3 编辑、复制、删除与启停任务"));
body.push(P("任务卡片上提供以下操作按钮：「编辑」打开任务编辑窗口修改参数；「复制」快速创建相同配置的任务副本；「删除」删除任务（需二次确认）；「▶ 立即」跳过定时计划立即播放该任务；以及启用/停用开关，停用的任务保留配置但不参与调度。"));

body.push(H2("4.4 播放控制与音量、均衡器"));
body.push(P("任务到点后软件自动开始播放，主界面播放器面板同步显示当前歌曲与进度。播放过程中可使用播放/暂停、上一首、下一首、停止按钮；拖动进度条可跳转播放位置（拖动中仅预览时间，松开后跳转）；拖动音量滑杆实时调节播放音量（0～100%）；均衡器面板提供 10 个频段（31Hz～16kHz）滑杆与多种预设（平坦、低音增强、高音增强、人声、电子、古典、流行、爵士、摇滚），调节立即生效。当定时任务指定了任务专属均衡器预设时，任务播放期间自动应用该预设，任务结束后自动还原全局设置。"));

body.push(H2("4.5 任务导入与导出"));
body.push(P("点击「导出」可将当前全部任务保存为 JSON 备份文件；点击「导入」选择备份文件，软件自动为导入任务分配新编号，逐个检查时间冲突：无冲突的任务加入列表，冲突的任务跳过，并在结果对话框中汇总成功与跳过明细。"));

body.push(H2("4.6 播放列表库"));
body.push(P("在任务编辑窗口中可将当前任务的全部歌曲「另存为播放列表」（可命名，同名覆盖）；也可从已保存的播放列表一键插入歌曲到当前任务。播放列表的删除同样在任务编辑窗口中完成。"));

body.push(H2("4.7 设置"));
body.push(P("点击主界面「⚙ 设置」打开设置窗口："));
body.push(table([
  ["设置项", "说明"],
  ["默认音量", "软件启动时的初始播放音量（0～100%），按 1% 步进"],
  ["淡入淡出", "定时任务开始播放时淡入、结束前淡出的时长（秒），可设为 0 关闭"],
  ["开机自动启动", "一键开关；开启后软件随 Windows 登录自动启动（写入当前用户注册表，无需管理员权限）"],
]));
body.push(P("所有设置自动保存并跨启动生效。"));

body.push(H2("4.8 系统托盘"));
body.push(P("点击主窗口关闭按钮时，软件不会退出，而是最小化到系统托盘并弹出气泡提示，定时任务继续在后台运行。双击托盘图标或点击托盘菜单「打开主界面」恢复窗口；托盘菜单还提供「停止播放」「关于」「退出」（退出将彻底结束程序，定时任务停止调度）。软件具备单实例保护，重复启动时不会产生第二个实例。"));

body.push(H2("4.9 睡眠唤醒"));
body.push(P("电脑从睡眠或休眠恢复后，软件自动重新校准所有任务的下一次触发时间，无需人工干预；如某次任务触发时刻恰逢睡眠状态错过，该次不补播，后续按计划正常执行。"));

body.push(new Paragraph({ children: [new PageBreak()] }));
body.push(H1("五、任务执行规则说明"));
body.push(li("触发精度：调度引擎以 500 毫秒为周期扫描，任务在设定开始时刻 1.5 秒内触发；"));
body.push(li("结束处理：到达结束时间强制停止；若设置了淡出时长，则在结束前开始音量渐弱；"));
body.push(li("任务重叠：同一时刻只允许一个任务播放，新任务触发时自动停止上一个任务；"));
body.push(li("一次性任务：触发后自动标记为「已完成」，次日不再重复；"));
body.push(li("曲目容错：播放列表中缺失或损坏的文件将被自动跳过并记录日志，不影响后续曲目。"));

body.push(H1("六、常见问题"));
body.push(table([
  ["问题", "处理方法"],
  ["双击程序无反应", "优先使用多文件绿色版；查看 %APPDATA%\\MusicScheduler\\startup.log 定位原因；从压缩包解压的先执行解除锁定；如被杀毒软件拦截请加入白名单"],
  ["任务到点没有播放", "确认任务已启用且曲目列表非空；确认结束时间晚于开始时间；确认软件正在运行（托盘有图标）；一次性任务当天已触发过则显示「已完成」"],
  ["保存任务提示冲突", "按提示调整与其他任务重叠的时间区间后重新保存"],
  ["没有声音", "检查系统音量与软件音量滑杆；检查均衡器增益是否设置过低；确认曲目文件可正常播放"],
  ["修改默认音量后重启仍不生效", "升级到 V1.0.0 修复版本（旧版本存在启动时覆盖设置的问题）"],
]));

body.push(H1("七、数据存储位置"));
body.push(table([
  ["数据", "位置"],
  ["任务数据", "%APPDATA%\\MusicScheduler\\tasks.json"],
  ["播放列表库", "%APPDATA%\\MusicScheduler\\playlists.json"],
  ["软件设置", "%APPDATA%\\MusicScheduler\\settings.json"],
  ["运行日志", "%APPDATA%\\MusicScheduler\\log.txt"],
  ["启动日志", "%APPDATA%\\MusicScheduler\\startup.log"],
  ["开机自启", "注册表 HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\\MusicScheduler"],
]));

body.push(new Paragraph({ children: [new PageBreak()] }));
body.push(H1("八、版权信息"));
body.push(P(`${SOFT_NAME} ${VERSION}`));
body.push(P(`著作权人：${OWNER}`));
body.push(P("本软件及全部文档受中华人民共和国著作权法及国际版权公约保护，未经著作权人书面许可，任何单位或个人不得以任何形式复制、传播、反编译或用于商业用途。"));

// ---------- 组装 ----------
const headerText = `${SOFT_NAME} ${VERSION}  用户手册`;
const doc = new Document({
  numbering: {
    config: [{
      reference: "num",
      levels: [{
        level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT,
        style: { paragraph: { indent: { left: 480, hanging: 360 } } }
      }]
    }]
  },
  styles: {
    default: { document: { run: { font: zhFont, size: 21 } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal",
        run: { size: 30, bold: true, font: "SimHei" },
        paragraph: { spacing: { before: 280, after: 160 }, outlineLevel: 0 } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal",
        run: { size: 24, bold: true, font: "SimHei" },
        paragraph: { spacing: { before: 200, after: 120 }, outlineLevel: 1 } }
    ]
  },
  sections: [{
    properties: {
      page: {
        size: { width: 11906, height: 16838 },
        margin: { top: 1100, right: 1422, bottom: 1100, left: 1422, header: 600, footer: 600 }
      }
    },
    headers: {
      default: new Header({
        children: [new Paragraph({
          alignment: AlignmentType.CENTER,
          border: { bottom: { style: "single", size: 4, color: "888888" } },
          children: [new TextRun({ text: headerText, font: { ascii: "SimSun", eastAsia: "宋体" }, size: 18 })]
        })]
      })
    },
    footers: {
      default: new Footer({
        children: [new Paragraph({
          alignment: AlignmentType.CENTER,
          children: [
            new TextRun({ text: "第 ", font: { ascii: "SimSun", eastAsia: "宋体" }, size: 18 }),
            new TextRun({ children: [PageNumber.CURRENT], size: 18 }),
            new TextRun({ text: " 页", font: { ascii: "SimSun", eastAsia: "宋体" }, size: 18 })
          ]
        })]
      })
    },
    children: [...cover, ...body]
  }]
});

Packer.toBuffer(doc).then(buf => {
  fs.writeFileSync(OUT, buf);
  console.log(`已生成：${OUT}`);
});
