@echo off
chcp 65001 > nul
echo =======================================================
echo PING UYARI - GITHUB REPO GÜNCELLEME BETİĞİ
echo =======================================================
echo.
echo 1. Git değişiklikleri çekiliyor...
git pull origin main --rebase
echo.
echo 2. Güncellemeler GitHub'a yükleniyor...
git push -u origin main
echo.
echo İŞLEM TAMAMLANDI!
echo.
pause
