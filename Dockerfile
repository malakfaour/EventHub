# One Dockerfile for both web apps. Build with:  docker build --build-arg APP=EventHub.Web -t eventhub-web .
# The build context is the repository root because both apps reference EventHub.Contracts and EventHub.Messaging.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG APP=EventHub.Web
WORKDIR /src
COPY . .
RUN dotnet publish "$APP/$APP.csproj" -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
ARG APP=EventHub.Web
ENV APP_DLL=$APP.dll
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "dotnet $APP_DLL"]
