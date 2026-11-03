@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0fake-events.ps1" %*
