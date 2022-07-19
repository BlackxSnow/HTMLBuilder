read -p "Select target (1 - Win64, 2 - Linux64) " choice
platform=""
case $choice in
    1 ) platform="win-x64" ;;
    2 ) platform="linux-x64" ;;
    * ) echo "Invalid input. " ;;
esac

dotnet publish HTMLBuilder.sln -p:PublishSingleFile=true -r $platform -c Release --self-contained false