# Planora Professional Showcase — editable project

This project renders the 60-second silent Planora promo at 1920x1080, 60 FPS, H.264.
All application content comes from the supplied captures. The browser mockup, shadows,
section captions, transitions, and original Planora logo are presentation layers.

## Contents
- render.py: editable compositor and video export.
- timeline.json: scene order, duration, source selection, crop, and captions.
- assets/: original supplied screenshots/recordings plus the original application logo.
- requirements.txt: Python dependencies.

## Render
Install Python 3.11 or newer, then run:

    python -m pip install -r requirements.txt
    python render.py --output Planora_Professional_Showcase.mp4

Generate contact sheets and full-size review frames with:

    python render.py --preview

Segoe UI is used on Windows. DejaVu Sans is the fallback on Linux. FFmpeg is supplied
through imageio-ffmpeg. No network access is needed once dependencies are installed.
The edit is a reproducible Python/JSON project, not a Premiere or DaVinci Resolve file.

## Editorial decisions
The ZIP and Desktop/Planora Video folder were byte-for-byte identical. Other Planora
folders contained architecture diagrams and application assets, not extra UI captures.
The edit uses 13 screenshots, the public-site recording, a real sign-in frame from the
account recording, and the original Planora logo.

Excluded: the 404 screenshot (211942), empty create-project and create-backlog forms
(211520, 211631), and the second V-Model module-list screenshot (225251), which repeats
the overview. Password recovery, registration, reverse navigation and idle video
sections were trimmed. Source material is retained in assets for future edits.

Missing footage: dedicated Requirements list/editor; AI Requirement Generator and
quality-score results; generated SRS preview/save/export; QA review actions; issue
reporting interaction; an open Notifications panel; Kanban drag/drop. Testing/Issues
are shown in their captured empty states. Traceability shows the real incomplete
chain. The SRS screen shows its real controls, not a fabricated generated document.

The 60-second timeline includes each short transition within the outgoing shot.
The export contains no audio stream. Screen pixels are scaled/cropped proportionally;
text, data, buttons and application colors are not reconstructed or redesigned.
