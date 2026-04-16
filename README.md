# Music Player for osu!

[![GitHub release](https://img.shields.io/github/release-pre/Christopher-Hayes/osuplayer.svg)](https://github.com/Christopher-Hayes/osuplayer/releases/latest)
![](https://img.shields.io/github/repo-size/Christopher-Hayes/osuplayer)
![](https://img.shields.io/github/issues/Christopher-Hayes/osuplayer?color=red)
![](https://img.shields.io/github/contributors/Christopher-Hayes/osuplayer?color=blueviolet)
[![.NET Publish](https://github.com/Christopher-Hayes/osuplayer/actions/workflows/dotnet-publish.yml/badge.svg)](https://github.com/Christopher-Hayes/osuplayer/actions/workflows/dotnet-publish.yml)

An [osu!player](https://github.com/Founntain/osuplayer) fork.

This fork is solely focused on core music player features. Accounts / online features are absent.

Music Player for osu! is well, a music player for *osu!* for playing your osu! songs **without having to start osu!**.

### Differences from osu!player

🔥 **Removed**

- **Online accounts** / profiles:
  - Users page
  - Party page
  - Stats page
  - Beatmaps page

✨ **Added**

- Song lists **show cover images** (optional setting).
- The **song sort** dropdown from settings was also added to the "Songs" view.
- Added support for **media keys** on Linux.
- The song list **highlights** the currently playing song.
- Song control icon buttons now have **tooltips.**

🏗️ **Changed**

- The **repeat button** was simplified to only focus on whether songs repeat. Previously it would enable/disable playlists, which could be confusing.
- **Song cover image** now always shows to the left of the song title (regardless of app background).
- Clicking on the cover image now shows a **full-size song cover image**.
- **Playback speed** snaps to 0.1x increments and shows the actual playback speed.

🐞 **Fixed**

- **Last.FM Scrobble:** instead of scrobbling instantly, it waits until you've listen to at least 50% of the song.
- **Previous song** button when shuffling would go to a random song rather than the last played song.

```bash
---- The original README continues below ----
```

## ☝️ Requirements

#### osu!player requirements
✔️ A working computer  
✔️ .NET 8 or later installed  
✔️ osu! installed with an **osu!.db file** or **osu!lazer client.realm** *(Beatmaps imported in osu!)*  
✔️ An internet connection if you want to use your osu!player plus profile

#### Download osu!player
To download the osu!player head to our [release](https://github.com/Christopher-Hayes/osuplayer/releases) section to download the latest release.  
You can also build the project for yourself; see the section below!

## ⚒️Building the project
 - Clone / Download the source
 - Open it with Visual Studio, Visual Studio Code, Rider or an IDE of your choice that supports C# and .NET
 - Run `dotnet restore` (or IDE tools) to restore all packages and dependencies
 - Build/Run the project

## 👋 Contributing to the project
#### ☝️Requirements
 - .NET 8 SDK
 - [Avalonia .NET Templates](https://github.com/AvaloniaUI/avalonia-dotnet-templates)
 - [Check out the Avalonia getting started](https://github.com/AvaloniaUI/Avalonia#-getting-started)
 - *Have a **decent understanding of the internal osu!** structure and know osu! (the game) as well.*

#### 🚀 How to contribute
 - Make a fork of this repository
 - Implement your ideas and features
 - Make a pull request (PR) on this repository
 - Pray that I accept your PR 😂 (I'm joking)
 - Profit 📈

> [!WARNING]\
> You should implement features that are asked for and not ones you like or think will be good additions.
A rule of thumb is: If you want a new feature, discuss it with us to see if it makes sense implement, if it does the feature may be added. So don't be afraid to ask!
**We appreciate your ideas and feedback!**

## 📦 Dependencies
| Dependency                                                        | Description                                       |
|-------------------------------------------------------------------|---------------------------------------------------|
| [AvaloniaUI](https://github.com/AvaloniaUI/Avalonia)              | The UI-Framework                                  |
| [FluentAvalonia](https://github.com/amwx/FluentAvalonia)          | UI-Framework Extensions                           |
| [ManagedBass](https://github.com/ManagedBass/ManagedBass)         | The Audio-Engine                                  |
| [discord-rpc-sharp](https://github.com/Lachee/discord-rpc-csharp) | Used to display Discord RPC                       |

## ✨ Special thanks
- ***SourRaindrop***: for creating a lot of custom images and assets like our logo
- ***You, the user***: for using this project and helping us improving it and simply enjoying your osu! music

## 🪛 Features that are missing to have the full osu!player plus feature set

#### 🔧 Features with lower priority
- [x] Audio-Equalizer 
- [x] Miniplayer to save some space
- [x] Export songs to directory    
- [ ] Localization 
- [ ] Hotkey support  
- [ ] Synced play via osu!player API  

#### 🎱 Stop asking for it
❌ Steering wheel support

## 🎵 We are the creators of the osu!player (about us)

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

## 📫 Contact
- [✉️ 7@founntain.dev](mailto:7@founntain.dev)
- 📣 [Discord](https://discord.gg/RJQSc5B)

## ⭐ Star History

[![Star History Chart](https://api.star-history.com/svg?repos=founntain/osuplayer&type=Date)](https://star-history.com/#founntain/osuplayer&Date)

## 🖼️ Screenshots

![image](https://github.com/user-attachments/assets/4d5c7ba2-1b40-4c1e-aeab-867f5d72b0da)  
![image](https://github.com/user-attachments/assets/e9894f29-8958-47f7-95ff-1af0261b1726)  
![image](https://github.com/user-attachments/assets/43c30551-3b51-408f-a464-9521798a166d)

### Miniplayer  
![image](https://github.com/user-attachments/assets/edb674d3-edc7-4457-81c8-60b833115fcc)

