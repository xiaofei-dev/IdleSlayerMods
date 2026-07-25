from __future__ import annotations

import importlib.util
import zipfile
from pathlib import Path

from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.platypus import (
    BaseDocTemplate,
    Frame,
    Image,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
)


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "output" / "pdf"
VERSION = "1.0.1"
PAGE_WIDTH, PAGE_HEIGHT = A4

SHARED_GENERATOR = (
    ROOT.parent / "AutoClimber" / "tools" / "generate_documentation_pdfs.py"
)
spec = importlib.util.spec_from_file_location(
    "automation_suite_documentation",
    SHARED_GENERATOR,
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Unable to load shared documentation styles: {SHARED_GENERATOR}")
shared = importlib.util.module_from_spec(spec)
spec.loader.exec_module(shared)

# Reuse the suite's Markdown renderer, fonts, colors, and typography while
# keeping AutoBonusRunner filenames, metadata, headers, and cover text local.
shared.ROOT = ROOT
shared.OUTPUT = OUTPUT
shared.VERSION = VERSION


class DocumentationTemplate(BaseDocTemplate):
    def __init__(self, filename: Path, document_name: str):
        self.document_name = document_name
        super().__init__(
            str(filename),
            pagesize=A4,
            leftMargin=22 * mm,
            rightMargin=22 * mm,
            topMargin=20 * mm,
            bottomMargin=18 * mm,
            title=document_name,
            author="Tashi",
            subject="AutoBonusRunner documentation for Idle Slayer",
        )
        frame = Frame(
            self.leftMargin,
            self.bottomMargin,
            self.width,
            self.height,
            id="normal",
        )
        self.addPageTemplates(
            PageTemplate(id="main", frames=[frame], onPage=self.decorate)
        )

    def decorate(self, canvas, doc):
        if doc.page == 1:
            return
        canvas.saveState()
        canvas.setStrokeColor(shared.LINE)
        canvas.setLineWidth(0.5)
        canvas.line(
            22 * mm,
            PAGE_HEIGHT - 13 * mm,
            PAGE_WIDTH - 22 * mm,
            PAGE_HEIGHT - 13 * mm,
        )
        canvas.setFont(shared.FONT, 7.5)
        canvas.setFillColor(shared.MUTED)
        canvas.drawString(22 * mm, PAGE_HEIGHT - 10 * mm, "AutoBonusRunner")
        canvas.drawRightString(
            PAGE_WIDTH - 22 * mm,
            PAGE_HEIGHT - 10 * mm,
            self.document_name,
        )
        canvas.line(22 * mm, 12 * mm, PAGE_WIDTH - 22 * mm, 12 * mm)
        canvas.drawString(
            22 * mm,
            8 * mm,
            "Idle Slayer community mod documentation",
        )
        canvas.drawRightString(
            PAGE_WIDTH - 22 * mm,
            8 * mm,
            f"Page {doc.page}",
        )
        canvas.restoreState()


def cover(title: str, subtitle: str):
    flow = []
    banner = ROOT / "Assets" / "banner.png"
    if banner.exists():
        flow.extend(
            [
                Image(str(banner), width=165 * mm, height=47.2 * mm),
                Spacer(1, 21 * mm),
            ]
        )
    else:
        flow.append(Spacer(1, 50 * mm))
    flow.extend(
        [
            Paragraph("AutoBonusRunner", shared.STYLES["cover_title"]),
            Paragraph(title, shared.STYLES["cover_subtitle"]),
            Spacer(1, 8 * mm),
            Table(
                [[""]],
                colWidths=[45 * mm],
                rowHeights=[1.2 * mm],
                style=[
                    (
                        "BACKGROUND",
                        (0, 0),
                        (-1, -1),
                        shared.CYAN,
                    )
                ],
            ),
            Spacer(1, 8 * mm),
            Paragraph(subtitle, shared.STYLES["cover_meta"]),
            Paragraph(
                f"Version {VERSION}  |  Tashi",
                shared.STYLES["cover_meta"],
            ),
            PageBreak(),
        ]
    )
    return flow


def build_user_guide() -> Path:
    target = OUTPUT / f"AutoBonusRunner-User-Guide-{VERSION}.pdf"
    story = cover(
        "User Guide",
        "Installation, modes, controls, rewards, and everyday use",
    )
    story.extend(
        shared.markdown_to_flowables(
            ROOT / "USER_GUIDE.md",
            omit_title=True,
        )
    )
    DocumentationTemplate(target, "User Guide").build(story)
    return target


def build_complete_manual() -> Path:
    target = OUTPUT / f"AutoBonusRunner-Complete-Manual-{VERSION}.pdf"
    story = cover(
        "Complete Manual",
        "Routing, jumping, recovery, modes, rewards, configuration, and diagnostics",
    )
    story.extend(
        shared.markdown_to_flowables(
            ROOT / "MANUAL.md",
            omit_title=True,
        )
    )
    for chapter in sorted((ROOT / "docs").glob("[0-9][0-9]-*.md")):
        story.append(PageBreak())
        story.extend(shared.markdown_to_flowables(chapter))
    DocumentationTemplate(target, "Complete Manual").build(story)
    return target


def build_documentation_zip(outputs: list[Path]) -> Path:
    target = OUTPUT / f"AutoBonusRunner-Documentation-{VERSION}.zip"
    with zipfile.ZipFile(
        target,
        mode="w",
        compression=zipfile.ZIP_DEFLATED,
        compresslevel=9,
    ) as archive:
        for output in outputs:
            archive.write(output, arcname=output.name)
    return target


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    outputs = [build_user_guide(), build_complete_manual()]
    documentation_zip = build_documentation_zip(outputs)
    for output in [*outputs, documentation_zip]:
        print(output)


if __name__ == "__main__":
    main()
