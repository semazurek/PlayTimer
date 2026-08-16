<h1> <img src="https://github.com/user-attachments/assets/66144689-e9db-4eda-95a1-86899f3c3cc4" width="48"> PlayTimer </h1>

<a href="#"><img src="https://img.shields.io/badge/RELEASE-v1.0-blue?style=for-the-badge&"></a>
<a href="#"><img src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white"></a>
<a href="#"><img src="https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white"></a>
<a href="https://www.buymeacoffee.com/semazurek" target="_blank"><img src="https://img.shields.io/badge/buymeacoffee-27ae60?style=for-the-badge&logo=buymeacoffee&logoColor=white"></a>

An app for limiting gaming time that doesn't track total computer uptime, but rather the time the game is running and component usage indicating active gameplay.

Download link: <a href="https://minhaskamal.github.io/DownGit/#/home?url=https://github.com/semazurek/PlayTimer/blob/main/bin/Debug/PT2.exe"> PT2.exe </a>

## What it does

1) Launching the application requires a password each time, or the initial creation of one.

2) After pressing START, closing the window with the X button will cause the application to run in the background, with a hidden icon in the system tray.
Even if the user restarts the computer, the playtime counter is temporarily saved locally, and it will automatically start up with Windows until the "STOP" button is pressed.

3) 15 minutes before the end, the program will display a message warning that the gaming session is about to finish, and it will shut down the computer once the time has elapsed.

4) The folder containing the password is hidden, and the password file is hashed: %programdata%\PlayTimer (Remove PT.log file to set new password)

## First look

<img src="https://github.com/user-attachments/assets/c41781da-cf33-474f-a0b5-d63ce9ac4782" width="760">
