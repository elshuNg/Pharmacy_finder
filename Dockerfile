FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PharmacyFinder.API.csproj .
RUN dotnet restore

COPY . .
RUN mkdir -p tessdata \
    && curl -fsSL -o tessdata/eng.traineddata \
       https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata
 
RUN dotnet publish PharmacyFinder.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libleptonica-dev \
        libtesseract-dev \
        tesseract-ocr \
        curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY --from=build /src/tessdata ./tessdata

ENV ASPNETCORE_URLS=http://+:8080
ENV TESSDATA_PREFIX=/app/tessdata

RUN mkdir -p /app/x64 \
    && ln -sf /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so \
    && ln -sf "$(ls /usr/lib/x86_64-linux-gnu/liblept*.so* 2>/dev/null | head -1)" /app/x64/libleptonica-1.82.0.so \
    && ln -sf "$(ls /usr/lib/x86_64-linux-gnu/libtesseract.so* 2>/dev/null | head -1)" /app/x64/libtesseract50.so

EXPOSE 8080

ENTRYPOINT ["dotnet", "PharmacyFinder.API.dll"]
