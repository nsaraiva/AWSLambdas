FROM public.ecr.aws/lambda/dotnet:5.0 AS base

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim as build

WORKDIR /source

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish --no-restore -c Release -o /app/publish

FROM base AS final
WORKDIR /var/task
COPY --from=build /app/publish .
CMD ["HelloWorldLambdaContainer::HelloWorldLambdaContainer.Function::FunctionHandler"]