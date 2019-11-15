# FFSend.Net
Firefox Send via .VB.Net

Upload Files to Firefox Send Service via Websocket.

This is a work in progress.

you need to download Newtonsoft.Json and BouncyCastle.Crypto
separately and copy it in your application folder.

Whats working:
[X] All encryption routines.
[X] Raw Websocket implementation.
[X] Upload Files and Metadata (Get the right response)

Whats not:
[X] Open the Link in the Browser and get the file back.

Please support and give hints why this is not working.
(i think its only a small issue with the encryption)

The most knowledge comes from this github-project
which is not working anymore (because of the socket
communication instead of Ajax i think):
https://github.com/shubzghadge/firefoxsenduploader
