FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ./*.sln ./
COPY */*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p ${file%.*}/ && mv $file ${file%.*}/; done

RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o /out /src/eLime.NetDaemonApps/eLime.NetDaemonApps.csproj

# Install dotnet debug tools
## https://www.mytechramblings.com/posts/profiling-a-net-app-with-dotnet-cli-diagnostic-tools/
## https://medium.com/@nnilesh7756/copy-directories-and-files-to-and-from-kubernetes-container-pod-19612fa74660
#RUN dotnet tool install --tool-path /tools dotnet-trace \
 #&& dotnet tool install --tool-path /tools dotnet-counters \
 #&& dotnet tool install --tool-path /tools dotnet-dump \
 #&& dotnet tool install --tool-path /tools dotnet-gcdump

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine AS run

WORKDIR /tools
COPY --from=build /tools .

WORKDIR /app
COPY --from=build /out .

ENTRYPOINT ["dotnet", "eLime.NetDaemonApps.dll"]
