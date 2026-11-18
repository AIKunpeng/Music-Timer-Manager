// 软著申请材料：源代码文档生成器
// 规则：每页 50 行，页眉标注软件名称+版本号，页脚页码；总量超过 60 页时取前 30 页 + 后 30 页。
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, Header, Footer, AlignmentType,
  PageNumber, PageBreak, ShadingType
} = require("docx");

const ROOT = path.resolve(__dirname, "..");
const SRC = path.join(ROOT, "MusicScheduler");
const OUT = path.join(__dirname, "源代码文档-音乐定时管家软件V1.0.0.docx");

const SOFT_NAME = "音乐定时管家软件";
const VERSION = "V1.0.0";
const LINES_PER_PAGE = 50;
const MAX_PAGES = 60;

// ---------- 1. 收集源码文件（排除 obj/bin/Assets） ----------
function collect(dir) {
  const out = [];
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) {
      if (/^(obj|bin|Assets)$/i.test(e.name)) continue;
      out.push(...collect(full));
    } else if (/\.(cs|xaml)$/i.test(e.name) && !/\.g\.cs$/i.test(e.name)) {
      out.push(full);
    }
  }
  return out;
}
const files = collect(SRC).sort((a, b) =>
  a.slice(SRC.length + 1).localeCompare(b.slice(SRC.length + 1))
);

// ---------- 2. 展开为代码行 ----------
const lines = [];
for (const f of files) {
  const rel = f.slice(SRC.length + 1);
  lines.push(`// ═══════════ 文件：${rel} ═══════════`);
  const content = fs.readFileSync(f, "utf8");
  for (const l of content.replace(/\r\n/g, "\n").replace(/\r/g, "\n").split("\n")) {
    lines.push(l.replace(/\t/g, "    ").trimEnd());
  }
  // 去掉文件末尾多余空行（保留一个分隔空行）
  while (lines.length && lines[lines.length - 1] === "") lines.pop();
  lines.push("");
}

// ---------- 3. 按 50 行/页 分页 ----------
const rawPages = [];
for (let i = 0; i < lines.length; i += LINES_PER_PAGE) {
  rawPages.push(lines.slice(i, i + LINES_PER_PAGE));
}

let pages;
let mode;
if (rawPages.length <= MAX_PAGES) {
  pages = rawPages;
  mode = `全文共 ${rawPages.length} 页，不足 60 页，全部提交`;
} else {
  pages = [...rawPages.slice(0, 30), ...rawPages.slice(-30)];
  mode = `全文共 ${rawPages.length} 页，按规定提交前 30 页 + 后 30 页共 ${pages.length} 页`;
}
console.log(`源码文件数：${files.length}，总行数：${lines.length}，总页数(50行/页)：${rawPages.length}`);
console.log(`提交策略：${mode}`);

// ---------- 4. 生成 docx ----------
const codeFont = { ascii: "Consolas", eastAsia: "宋体", hAnsi: "Consolas" };

function codeParagraph(text) {
  return new Paragraph({
    spacing: { before: 0, after: 0, line: 216, lineRule: "auto" }, // 9pt 行距
    children: [new TextRun({ text: text || " ", font: codeFont, size: 17 })] // 8.5pt
  });
}

const children = [];
pages.forEach((pageLines, idx) => {
  if (idx > 0) children.push(new Paragraph({ children: [new PageBreak()] }));
  pageLines.forEach(l => children.push(codeParagraph(l)));
});

const headerText = `${SOFT_NAME} ${VERSION}  源程序`;
const doc = new Document({
  styles: { default: { document: { run: { font: codeFont, size: 17 } } } },
  sections: [{
    properties: {
      page: {
        size: { width: 11906, height: 16838 },           // A4
        margin: { top: 900, right: 850, bottom: 900, left: 850, header: 500, footer: 500 }
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
            new TextRun({ text: " 页 共 ", font: { ascii: "SimSun", eastAsia: "宋体" }, size: 18 }),
            new TextRun({ children: [PageNumber.TOTAL_PAGES], size: 18 }),
            new TextRun({ text: " 页", font: { ascii: "SimSun", eastAsia: "宋体" }, size: 18 })
          ]
        })]
      })
    },
    children
  }]
});

Packer.toBuffer(doc).then(buf => {
  fs.writeFileSync(OUT, buf);
  console.log(`已生成：${OUT}`);
});
