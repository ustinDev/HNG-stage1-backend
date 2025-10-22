# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY ./StringAna/*.sln .
RUN mkdir -p StringAna
COPY ./StringAna/*.csproj ./StringAna
COPY ./StringAna/*.csproj .
RUN dotnet restore StringAna.sln

# copy everything else and build app
COPY ./StringAna ./StringAna 
RUN dotnet publish StringAna/StringAna.csproj -c Release -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "StringAna.dll"]