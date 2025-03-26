# DotNetFlix

## Introduction
DotNetFlix (**not** affiliated with NetFlix) is a .NET solution that provides simple, robust multimedia hosting services.

## Features
- SQLite for settings & metadata
- AWS S3 for bulk media storage
- FFmpeg for transcoding
- Server-side (local) encryption of stored media
- Rapid seeking via block-based streaming architecture and range request support
- Caches media blocks for quick playback and minimization of S3 transfer fees

## Demonstration
_TODO: Video of basic use_

## Installation
1. Designate a locally-networked server machine for install. This can be anything that .NET runs on.
1. Signup for an AWS account (todo link).
1. Create an AWS S3 bucket & user credential (todo extended steps).
1. Download or clone this repository to the server.
1. Install the .NET SDK from (todo link) on the server.
1. Execute `dotnet run` in the checkout folder.
1. Open a web browser to `http://localhost` and follow the final setup instructions.
1. Ensure firewall access is open on TCP 80 to allow other local machines to access the server.