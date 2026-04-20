# Music Player for osu!

[![GitHub release](https://img.shields.io/github/release-pre/Christopher-Hayes/osuplayer.svg)](https://github.com/Christopher-Hayes/osuplayer/releases/latest)
![](https://img.shields.io/github/repo-size/Christopher-Hayes/osuplayer)
![](https://img.shields.io/github/issues/Christopher-Hayes/osuplayer?color=red)
![](https://img.shields.io/github/contributors/Christopher-Hayes/osuplayer?color=blueviolet)
[![.NET Publish](https://github.com/Christopher-Hayes/osuplayer/actions/workflows/dotnet-publish.yml/badge.svg)](https://github.com/Christopher-Hayes/osuplayer/actions/workflows/dotnet-publish.yml)

An [osu!player](https://github.com/Founntain/osuplayer) fork. This is a music player for playing the songs you've downloaded in the game, osu!

This fork is solely focused on core music player features. Accounts / online features are absent.

<img width="1424" height="967" alt="Screenshot of the app playing the song 'Dance the Night Away' from the k-pop band, TWICE. The app uses a blurred background of the song artwork, with a dark semi-transparent UI design." src="https://github.com/user-attachments/assets/8702bb6c-ef79-4b05-9fa0-8888354cdd5d" />


## Differences from osu!player

✨ **Added**

- A **new "Artists" page**, making it easier to play songs from a specific artist.
- **Cover images** are added to the song list.
- The **song sort** dropdown from settings was also added to the "Songs" view.
- Added support for **media keys** on Linux.
- The song list **highlights** the currently playing song.
- Song control icon buttons now have **tooltips.**
- Clicking on the cover image now shows a **full-size song cover image**.

🔥 **Removed**

- **Online accounts / profiles** - Users page, Party page, Stats page, Beatmaps page

🏗️ **Tweaked**

- The **repeat button** was simplified to only focus on whether songs repeat. Previously it would enable/disable playlists, which could be confusing. How playlists work is still a WIP.
- **Song cover image** now always shows to the left of the song title (regardless of app background).
- **Playback speed** snaps to 0.1x increments and shows the actual playback speed.

🐞 **Fixed**

- **Last.FM Scrobble:** instead of scrobbling instantly, it waits until you've listen to at least 50% of the song.
- **Previous song** button when shuffling would go to a random song rather than the last played song.

## Install

You can either go to the ["Releases" GitHub page](https://github.com/Christopher-Hayes/osuplayer/releases) to download a build, or build it yourself following the instructions below. Of course this music player depends on you already having osu! installed with songs downloaded.

**Supported platforms:** Windows, Linux, Mac OS

## Development

### Pre-requisistes

- .NET **10** SDK

### Dev Tooling

- An Avalonia account is recommended for some dev tools, but not required.

### Build

1. Clone project.
2. Open in an IDE like Visual Studio, VS Code, or JetBrains Rider.
3.  Run `dotnet restore` to restore all packages and dependencies.
4.  Build the project with `dotnet build` and then run it with `dotnet run --project OsuPlayer`

### Dependencies

| Dependency                                                        | Description                                       |
|-------------------------------------------------------------------|---------------------------------------------------|
| [AvaloniaUI](https://github.com/AvaloniaUI/Avalonia)              | The UI-Framework                                  |
| [FluentAvalonia](https://github.com/amwx/FluentAvalonia)          | UI-Framework Extensions                           |
| [ManagedBass](https://github.com/ManagedBass/ManagedBass)         | The Audio-Engine                                  |
| [discord-rpc-sharp](https://github.com/Lachee/discord-rpc-csharp) | Used to display Discord RPC                       |

## Original (upstream) creators of osu!player

**SourRaindrop**: for creating a lot of custom images and assets like our logo

### 🦊 Founntain

<a href="https://github.com/Founntain">
  <img style="border-radius: 50%;" align="right" width=200 height=200 src="https://osuplayer.founntain.dev/user/getProfilePicture?id=68c561ec-2313-43bc-8e1b-4227a2936e35" />
</a>
Hey, there my name is Founntain!
A bit about myself: I'm currently `currentYear - 1999` years old and from Germany. 
Currently, I am working for a medium-sized software development company as Software Consultant.

**Languages I use:**
+ C# *(For most of my projects)*
+ HTML, CSS and Typescript *(for web stuff)*
+ Java *(mostly for Minecraft plugin development)*

In 2016 I had my first programming contact at my IT-School. There we mostly developed in Java, but all stuff that we programmed in Java I tried to implement in C#
while learning it on my own.  

In 2017 I started development on the first versions of the osu!player in WPF and .NET-Framework 4.6. It looked bad, it felt bad and badly performed.
But let's be honest what do expect from someone who never used WPF at all and did not have much C# experience? 
On the 1st of November 2017, the first version of the osu!player was released on the osu! forum.  

After a while, Cesan joined me and we started working on it together now and I'm grateful for that. *Thanks buddy*

### 🌸 Cesan

<a href="https://github.com/Cesan">
  <img style="border-radius: 50%;" align="right" width=200 height=200 src="https://osuplayer.founntain.dev/user/getProfilePicture?id=8499175c-c7a6-40ae-ae96-bd6d3902c275" />
</a>
Hi, I'm Cesan. You can also call me Caro if you want ^^

I'm a self-taught C# dev and currently studying Applied Computer Sciences at University.
I also work as an embedded C dev in the meantime.

I mostly use C# for everything I do because I think it's the most versatile and practical language for desktop development.
In university, I learned C and Java. However I would never use Java personally.

When I joined the development team of the osu!player, I mostly did design stuff in WPF as I understood it best, but now we both do more or less the same stuff because we have quite some experience with the osu!player by now, to make the player look and feel like how it is today.

Thanks for reading and have fun with the player, cheers.
