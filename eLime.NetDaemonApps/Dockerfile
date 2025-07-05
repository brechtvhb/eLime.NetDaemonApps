FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS run

#WORKDIR /tools
#COPY --from=build /tools .

WORKDIR /app
RUN apk add --no-cache icu-libs
RUN apk add --no-cache icu-data-full
RUN apk add --no-cache tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /out .

ENTRYPOINT ["dotnet", "eLime.NetDaemonApps.dll"]
