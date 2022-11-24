FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.16 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ./*.sln ./
COPY */*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p ${file%.*}/ && mv $file ${file%.*}/; done

RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o /out /src/eLime.NetDaemonApps/eLime.NetDaemonApps.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine3.16 AS run
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "eLime.NetDaemonApps.dll"]
