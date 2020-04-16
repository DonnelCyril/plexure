### ItineraryManager

#### Constructor
1. Dependencies are created inside the constructor, they should be passed in as arguments to the constructors.
2. Validate parameters for null references and throw null reference exception accordingly.

#### CalculateAirlinePrices
1. Validate that the *priceProviders* argument is not null or empty.
2. Do not block on *_dataStore.GetItinaryAsync* by reading the *Result* property.
Use the *await* keyword and change the method signature to return *Task<IEnumerable\<Quote>>*
    ```csharp
     var itinerary = await _dataStore.GetItinaryAsync(itineraryId)
    ```
3. Use a *ConcurrentBag\<Quote>* instead of *List\<Quote>* for the quotes collections as this is being populated from multiple thread.
   We could also use PLINQ here and do away with intermediary list altogether.
    ```csharp
    priceProviders
        .AsParallel()
        .SelectMany(provider => provider.GetQuotes(itinerary.TicketClass, itinerary.Waypoints));
    ```
   - Few things to consider here:
     - Care should be taken when executing this in an Asp.net environment. Each parallel operation will be executed in a seperate thread from the thread pool. While this would improve individual response times, the overall throughput of the system will be affected.
     - Check if *provider.GetQuotes* is CPU bound task. Typically this will be an *async* task and *Task.WhenAll* should be used:
        ```csharp
        var quotesFromEachProvider = await priceProviders
            .Select(provider => provider.GetQuotes(itinerary.TicketClass, itinerary.Waypoints));
        return quotesFromEachProvider.Select(quote => quote);
        ```
    In the above case I am assuming *provider.GetQuotes* is reaching out to an API or datastore and the method returns a *Task<IEnumerable\<Quotes>>*
    
4. Method should accept an optional *CancellationToken* parameter.This parameter should then be passed into the *_dataStore.GetItinaryAsync* method if it accepts a cancellation token. 
5. This token should also be used when fetching quotes from the *priceProviders*.

#### CalculateTotalTravelDistanceAsync
1. This method should accept an optional *CancellationToken* parameter.
This parameter should then be passed into the *_dataStore.GetItinaryAsync* method if it accepts a cancellation token. 
2. *itinerary.Waypoints.Count* could throw a null reference exception.
3. This method sequentially calls *_distanceCalculator.GetDistanceAsync* and blocks till the method call returns.
4. Issues 2 & 3 could be addressed as below.<br>
```csharp
        var wayPoints = itinerary.Waypoints ?? Enumerable.Empty<Waypoint();
        var wayPointRoutes = wayPoints.Zip(wayPoints.Skip(1), (fst, snd) = (fst, snd));
        var distanceForEachRoute = await Task.WhenAll(
            wayPointRoutes.Select(r = _distanceCalculator.GetDistanceAsync(r.fst, r.snd))
        );
        var totalDistance = distanceForEachRoute.Sum();
        return totalDistance;
```


#### FindAgent
1. This method doesnt seem to do anything with Itinerary, I would move it into a separate class dealing with TravelAgents.
2. This method has multiple responsibilities, I would split them to two methods possibly:
   - FindAgent (*Task\<TravelAgent> FindAgent(int id)*)
   - UpdateAgent (*Task FindAgent(int id)*)
3. Move to an async equivalent for *_dataStore.GetAgent*.
4. Also the logic to update phone number:
    - we only need to do so if the provided number is different from the existing phone number
    - we will also move to an async equivalent for *_dataStore.UpdateAgent*.


#### General
1. ItineraryManager could derive from an interface. This makes it easy for the consuming code to mock ItineraryManager in unit tests.
2. For the awaited tasks we could set *ConfigureAwait(false)*.
    - If the above piece of code only runs in ASP.NET core or Console app, we could leave it as it is as there is no *SynchronisationContext* defined for these environment.
    - If this will be part of a library, that will be consumed in ASP.NET framework or one of the UI frameworks, then it is recommended to follow the above convention.
3. My personal preference is to adopt a command query pattern than to have service classes like ItineraryManager. Below are few reasons why:
    - Service classes tend to be sticky and attract functionality. It is hard to define a boundary for a *Service* and determine which methods are included in a *Service* and which are not.
    - There will be a number of cross cutting concerns that we may need applied across the different service methods. (eg: Logging, Caching, Metrics, Retries etc.)
    - While we could add these to the method itself, this creates a lot of noise and hides the core functionality of these methods.
    - We could leverage an existing library called [MediatR](https://github.com/jbogard/MediatR) to assist with implementing the command query separation.
        - MediatR implements the well know mediator pattern, below is some psuedo code to demostrate the usage.

        ```csharp
            // consuming code
            var totalDistance = await _mediatr.Send(new GetTravelDistanceForItinerary(4372))

            // Handler for the message
            public class GetTravelDistanceForItineraryHandler {

                IMediatr _mediatR;
                public GetTravelDistanceForItineraryHandler(IMediatr mediatr){
                    _mediatR = mediatR;
                }

                public Task<double> Handle(GetTravelDistanceForItinerary message){
                    var itinerary = await _mediatR.Send(GetItinerary(message.Itinerary));
                    var wayPoints = itinerary.Waypoints ?? Enumerable.Empty<Waypoint();
                    var wayPointRoutes = wayPoints.Zip(wayPoints.Skip(1), (fst, snd) = (fst, snd));
                    var distanceForEachRoute = await Task.WhenAll(
                            wayPointRoutes.Select(r = _mediatR.Send( new GetDistanceBetweenWaypoints(r.fst, r.snd))
                    );
                    var totalDistance = distanceForEachRoute.Sum();
                    return totalDistance;
                }
            }
       ```

        - In my opinion, the above implementation has few benefits:
            - Classes become simpler and more focussed. It comprises solely of the business logic and removes any operational concerns like logging, caching etc.
            - We can compose larger queries from a number of smaller queries.
            - Number of dependencies to the constructor is reduced, making it easier for unit testing.
            - This patterns lends itself well in creating generic reusable components
            ```csharp
                _mediatR.Send(new GetTableStorageEntity("Customer",3425));
                _mediatR.Send(new GetBlobStorageFile("fileName"));
                _mediatR.Send(new GetKeyVaultSecret("secretKey"));
           ```
           I have had success building these reusable components and using them across several projects. They hide the underlying SDK specific code, keeping the consuming code very focussed and specific to the problem at hand.

        - *MediatR* provides hooks that allow us to add additional functionality between when a message is published to when it is handled by the message handler. This is similar to the *middleware* (*Behaviors* in MediatR terminology) concepts in frameworks like *Redux*.
        - This allows us to handle different cross cutting concerns we mentioned before. Below is some psuedo code that shows how caching is implemented for a usecase where a *TravelAgent* entity need to be fetched.

            ```csharp
            // We start off with defining a *GetTravelAgent* message.
            // The class also derives from a *CacheSettings* interface which defines cache specific properties.
            public class GetTravelAgent : ICacheSetting
            {
                public GetTravelAgent(int agentId)
                {
                    CacheKey = $"TravelAgent-{agentId}";
                }
                public string CacheKey { get; }
                public TimeSpan ExpiryPeriod { get; } = TimeSpan.FromDays(1); // this could also be made a constructor argument if required.
            }

            public interface ICacheSetting
            {
                string CacheKey { get; }
                TimeSpan ExpiryPeriod { get; }
            }

            // consuming code
            var travelAgent = await _mediatR.Send(new GetTravelAgent(3273));

            // The *CachingBehavior* class is registered with the MediatR pipeline.
            // It ensure that this *Handle* method is invoked before the actual message handler.
            public class CachingBehavior<TRequest,TResponse>
            {
                private readonly  ICacheService _cacheService;
                private readonly IMetricsService _metricsService;

                // some code removed for brevity.
                 
                protected override async Task<TResponse> Handle(TRequest request,  RequestHandlerDelegate<TResponse> next){

                    // We are only interested in handling message that has cache configuration (derived from the *CacheSetting* interface)
                    if (request is ICacheSetting cacheSetting) 
                    {
                        return Handle(next, cacheSetting, GetMetricsName(request));
                    }
                    return next();
                }

                private async Task<TResponse> Handle(RequestHandlerDelegate<TResponse> next, ICacheSetting cacheSetting, string metricsPrefix)
                {
                    if (_cacheService.TryGetValue(cacheSetting.CacheKey, out TResponse cachedValue))
                    {
                        _metricsService.IncrementCounter($"{metricsPrefix}_cache_hit_total");
                        return cachedValue; // this essentially short circuits the message, preventing the handler from being called.
                    }
                    var response = await next();
                    _cacheService.Set(cacheSetting.CacheKey, response, cacheSetting.ExpiryPeriod);
                    _metricsService.IncrementCounter($"{metricsPrefix}_cache_miss_total");
                    return response;
                }
            }
           ```

            - Any message that need to be cached will derive from *ICacheSetting*. 
            - A Message can derive from multiple such interfaces like *ILogConfig*, *IRetryConfig*.
            - A dedicated behaviour class will inspect each message and will cater for the intended behavior.

