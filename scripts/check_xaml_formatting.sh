#!/bin/sh

# XAML formatting check script
# Called by pre-commit hook

cd "$(dirname "$0")/.."

if [ -f ".ignore_format" ]; then
    echo ".ignore_format file found, skipping XAML Styler check"
    exit 0
fi

echo "Running XAML Styler..."
dotnet.exe xstyler -pd Orion

if [ $? -ne 0 ]; then
    echo ""
    echo "XAML Styler failed. Please fix formatting issues before committing."
    echo "  - Run: format.bat"
    echo "  - Or bypass with: git commit --no-verify"
    exit 1
fi

exit 0
