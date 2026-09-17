@echo off
chcp 65001 > nul
echo =======================================================
echo PING UYARI - GITHUB REPO GUNCELLEME BETIGI
echo =======================================================
echo.
echo 1. Git degisiklikleri cekiliyor...
git pull origin main --rebase
echo.
echo 2. Guncellemeler GitHub'a yukleniyor...
git push -u origin main
echo.
echo ISLEM TAMAMLANDI!
echo.
pause
