FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY QuotesApi/QuotesApi.csproj QuotesApi/
RUN dotnet restore QuotesApi/QuotesApi.csproj
COPY QuotesApi/ QuotesApi/
RUN dotnet publish QuotesApi/QuotesApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "QuotesApi.dll"]
