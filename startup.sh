#!/bin/bash
# Startup script for Azure App Service
# Устанавливает Python и зависимости для remove_cars_pro.py

PYTHON_DIR="/home/site/wwwroot/python"
PYTHON_MARKER="/home/python_installed.marker"
LOG_FILE="/home/LogFiles/python_setup.log"

echo "$(date) - Starting Python setup..." >> "$LOG_FILE"

# Проверяем, установлены ли зависимости (маркер файл)
if [ ! -f "$PYTHON_MARKER" ] || [ "$PYTHON_DIR/requirements.txt" -nt "$PYTHON_MARKER" ]; then
    echo "$(date) - Installing Python dependencies..." >> "$LOG_FILE"

    # Установка python3 и pip (на Azure Linux App Service python3 обычно уже есть)
    apt-get update -qq && apt-get install -y -qq python3 python3-pip python3-venv libgl1-mesa-glx libglib2.0-0 2>> "$LOG_FILE"

    # Создаём виртуальное окружение
    python3 -m venv /home/python_venv 2>> "$LOG_FILE"

    # Устанавливаем зависимости
    /home/python_venv/bin/pip install --no-cache-dir -r "$PYTHON_DIR/requirements.txt" 2>> "$LOG_FILE"

    if [ $? -eq 0 ]; then
        touch "$PYTHON_MARKER"
        echo "$(date) - Python dependencies installed successfully" >> "$LOG_FILE"
    else
        echo "$(date) - ERROR: Failed to install Python dependencies" >> "$LOG_FILE"
    fi
else
    echo "$(date) - Python dependencies already installed, skipping..." >> "$LOG_FILE"
fi

# Создаём рабочие директории для Python скрипта
mkdir -p /home/site/wwwroot/python/cars_input
mkdir -p /home/site/wwwroot/python/cars_output

echo "$(date) - Starting .NET application..." >> "$LOG_FILE"

# Запускаем .NET приложение
cd /home/site/wwwroot
dotnet CarRental.Api.dll
