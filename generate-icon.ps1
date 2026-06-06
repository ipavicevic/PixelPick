# PixelPick icon generator
# Renders Assets\icon.svg to icon.ico and all Store PNG assets.
# Edit Assets\icon.svg to change the design, then re-run this script.

dotnet run --project "$PSScriptRoot\tools\GenerateIcon\GenerateIcon.csproj" -- "$PSScriptRoot"
