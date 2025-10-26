В папке проекта:

Создайте пакет
dotnet pack --configuration Release

Установите как global tool
dotnet tool install --global --add-source ./nupkg Parfy
dotnet tool update --global --add-source ./nupkg Parfy
dotnet tool uninstall --global Parfy

Проверьте установку
dotnet tool list --global

Протестируйте
my-tool --help