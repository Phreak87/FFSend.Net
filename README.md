# FFSend.Net

Firefox Send via C#/VB.Net

Upload Files to Firefox Send Service via the Websocket Service
and access them via the Firefox send Webpage.

you need to download Newtonsoft.Json and BouncyCastle.Crypto
separately and copy it in your Bin folder.

This is just a demonstration-project. Uploading small files will work 
(Tested 5.Dez.2019 - V2/3). Bigger Files will not work because
of the own (simplest) Websocket implementation.

The most knowledge comes from this github-project
which is not working anymore (because of the socket
communication instead of Ajax i think):
https://github.com/shubzghadge/firefoxsenduploader
