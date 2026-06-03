@echo off
title AR Interior Design App - Mobile Run
color 0A

echo ===============================================
echo   AR Interior Design App - Mobile Testing
echo ===============================================
echo.

echo Your laptop IPv4 addresses:
echo -----------------------------------------------
ipconfig | findstr /i "IPv4"
echo -----------------------------------------------
echo.

echo Mobile par browser open karke ye URL lagao:
echo http://YOUR-IP:5000
echo.
echo Example:
echo http://192.168.100.30:5000
echo.

echo Starting project for laptop + mobile...
echo Please keep this window open while testing.
echo.

dotnet run --urls "http://0.0.0.0:5000"

echo.
echo Project stopped.
pause
