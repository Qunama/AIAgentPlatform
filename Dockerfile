FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ProjectAIAgent.Core/ProjectAIAgent.Core.csproj ProjectAIAgent.Core/
COPY ProjectAIAgent.Host/ProjectAIAgent.Host.csproj ProjectAIAgent.Host/

RUN dotnet restore ProjectAIAgent.Host/ProjectAIAgent.Host.csproj

COPY ProjectAIAgent.Core/ ProjectAIAgent.Core/
COPY ProjectAIAgent.Host/ ProjectAIAgent.Host/

RUN dotnet publish ProjectAIAgent.Host/ProjectAIAgent.Host.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "ProjectAIAgent.Host.dll"]