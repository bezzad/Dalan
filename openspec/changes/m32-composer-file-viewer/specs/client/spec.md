# Client

## ADDED Requirements

### Requirement: CLI-85 Enter like Telegram
In the message box and in the big editor (CLI-87), the client SHALL decide Enter by the pointer, read from `matchMedia("(pointer: coarse)")` at each key press and never from the user agent. Fine pointer: Enter SHALL send; Shift+Enter or Ctrl+Enter SHALL add a new line. Coarse pointer: Enter SHALL add a new line, and only the Send button SHALL send. While an IME is composing (`isComposing`), Enter SHALL never send.

#### Scenario: Computer
- **GIVEN** the pointer is fine and the box has "Hi"
- **WHEN** the user presses Enter
- **THEN** "Hi" is sent

#### Scenario: New line on a computer
- **GIVEN** the pointer is fine
- **WHEN** the user presses Shift+Enter, then Ctrl+Enter
- **THEN** nothing is sent and the box has two new lines, each removed by one undo

#### Scenario: Phone
- **GIVEN** the pointer is coarse and the box has "Hi"
- **WHEN** the user presses Enter
- **THEN** nothing is sent and the box has "Hi" and a new line; the Send button sends it

#### Scenario: IME
- **GIVEN** the pointer is fine and an IME is composing
- **WHEN** the user presses Enter
- **THEN** nothing is sent

### Requirement: CLI-86 The message box grows
The message box SHALL grow with its text up to `--compose-max-height` (40% of the screen height) and then scroll. After a send it SHALL be one row high again.

#### Scenario: Long text
- **WHEN** the user types 30 lines
- **THEN** the box is 40% of the screen high and scrolls

#### Scenario: After send
- **WHEN** the user sends 5 lines
- **THEN** the box is one row high and empty

### Requirement: CLI-87 Big editor
When the text in the box is more than 3 lines (wrapped lines count), a small expand button (two diagonal arrows) SHALL show in the corner of the box. It SHALL open a modal with the same text in a large box: full screen on a coarse pointer or a screen under 600px wide, otherwise large in the middle of the screen. The modal SHALL have Send and Close. Close and Esc SHALL close it and keep the text, its selection and its undo. CLI-85 SHALL apply in it. Ctrl+B and Ctrl+I (Cmd on macOS) SHALL put `**` and `_` around the selected text.

#### Scenario: Button shows
- **WHEN** the box has 3 lines, then 4
- **THEN** the expand button is hidden, then shown

#### Scenario: Same text
- **GIVEN** the box has 4 lines
- **WHEN** the user opens the editor, adds a line, and presses Esc
- **THEN** the box has the 5 lines and the editor is closed

#### Scenario: Send from the editor
- **WHEN** the user presses Send in the editor
- **THEN** the text is sent, the editor is closed, and the box is empty and one row high

#### Scenario: Bold
- **GIVEN** "hello world" with "world" selected in the editor
- **WHEN** the user presses Ctrl+B
- **THEN** the text is "hello **world**"

### Requirement: CLI-88 A draft per chat
Each chat SHALL keep its unsent text when the user opens another chat or reloads the page, kept only in this browser for this user. A send SHALL remove the draft; logout SHALL remove every draft. The box SHALL keep `dir="auto"`, the font and the theme.

#### Scenario: Switch chats
- **GIVEN** the user typed "half" in the chat with bot A
- **WHEN** they open bot B, then bot A again
- **THEN** bot B's box is empty and bot A's box has "half"

#### Scenario: Logout
- **GIVEN** drafts in two chats
- **WHEN** the user logs out
- **THEN** no draft is left in the browser

### Requirement: CLI-89 Photos in the chat
A photo, and a document of type `image/jpeg`, `image/png`, `image/gif` or `image/webp`, SHALL show as a picture in its message. A tap or click SHALL open a full-screen viewer with pinch zoom, double-tap zoom, pan while zoomed, Download and Close. Esc SHALL close it. Any other image type (for example SVG) SHALL stay a download.

#### Scenario: Picture sent as a document
- **WHEN** a bot sends `chart.png` with `sendDocument`
- **THEN** the message shows the picture, not a link

#### Scenario: SVG
- **WHEN** a bot sends `logo.svg` of type `image/svg+xml`
- **THEN** the message shows a download link

#### Scenario: Open and close
- **WHEN** the user taps the picture, then presses Esc
- **THEN** the viewer opens with Download and Close, then closes and the focus is back on the message

#### Scenario: Double tap
- **WHEN** the user double-taps the open picture, then double-taps again
- **THEN** it zooms in at that point, then back to its full size

### Requirement: CLI-90 HTML files open in the app
A document of type `text/html`, or of no clear type with a name ending `.html` or `.htm`, SHALL show as a card with its name, size and "Open". Open SHALL get a view link (REL-57) and show it full screen inside the app in `<iframe sandbox="allow-scripts allow-popups">`, never with `allow-same-origin`. Where the Fullscreen API works, the viewer SHALL use it; elsewhere (iPhone) it SHALL be a fixed full-screen layer inside the safe areas. The viewer SHALL have Close, Download and "Open in a new tab"; Esc SHALL close it. On load the frame SHALL get the focus, so the page's own keys (← →) and swipes work. When the viewer is off (REL-57), the card SHALL show only Download.

#### Scenario: Card
- **WHEN** a bot sends `sprint-review.html` of 1.2 MB
- **THEN** the message shows a card "sprint-review.html", "1.2 MB" and "Open"

#### Scenario: Open on a computer
- **GIVEN** the Fullscreen API works
- **WHEN** the user presses Open
- **THEN** the viewer is full screen with the frame focused, and the frame's `sandbox` is exactly `allow-scripts allow-popups`

#### Scenario: Open on an iPhone
- **GIVEN** the Fullscreen API does not work
- **WHEN** the user presses Open
- **THEN** a fixed layer covers the screen inside the safe areas, and the chat under it does not scroll

#### Scenario: Turn a page
- **GIVEN** the sample report is open in the viewer, in WebKit as an iPhone and in Chromium
- **WHEN** the user presses → and then swipes left
- **THEN** the report shows its second and then its third page

#### Scenario: Close
- **WHEN** the user presses Close, or Esc, or leaves full screen
- **THEN** the viewer and its frame are gone and the chat is as before

#### Scenario: Viewer off
- **GIVEN** no view host is set
- **WHEN** a bot sends `page.html`
- **THEN** the card shows Download and no Open

## MODIFIED Requirements

### Requirement: CLI-16 Files in the chat
The client SHALL let the user pick a file and send it with the text in the input as its caption (REL-28), and refuse a file over `max_file_size_mb` (REL-18) before sending. A message with a `file` SHALL show a photo or a picture document as an image (CLI-89), an HTML document as a card that opens in the app (CLI-90), audio and voice with an audio player, video with a video player, and anything else (PDF too) as a link with its name and size.

#### Scenario: Kind of view
- **WHEN** messages with a photo, a PNG document, an HTML document, a voice, a video and a PDF document are shown
- **THEN** they use an image, an image, an HTML card, an audio player, a video player and a link

#### Scenario: Too big
- **WHEN** the user picks a file over the limit
- **THEN** nothing is sent and a message says the file is too big
