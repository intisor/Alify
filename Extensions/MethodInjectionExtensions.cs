namespace Alify.Extensions
{
    /// <summary>
    /// Method Injection Extensions for Controllers and Razor Pages.
    /// 
    /// Benefits of Method Injection:
    /// 1. REDUCED MEMORY FOOTPRINT: Services are resolved only when actually needed
    /// 2. CLEANER CONSTRUCTORS: Avoids constructor bloat with 8+ dependencies
    /// 3. BETTER TESTABILITY: Each method can be tested with specific service mocks
    /// 4. PERFORMANCE OPTIMIZATION: Services created on-demand, not held in memory
    /// 5. SEPARATION OF CONCERNS: Dependencies tied to specific operations
    /// 6. CONDITIONAL RESOLUTION: Services only resolved when certain conditions are met
    /// 
    /// Usage Examples:
    /// - this.WithService<IMemoryCache>(cache => cache.Remove("key"));
    /// - await this.WithServiceAsync<LyricService>(async service => await service.GetLyricsAsync());
    /// - var result = this.WithService<LyricService, string>(service => service.Process());
    /// - var service = this.ResolveService<SpotifyService>();
    /// </summary>
    public static class MethodInjectionExtensions
    {
        #region Synchronous Method Injection

        /// <summary>
        /// Resolves a service on-demand for immediate use.
        /// Best for: Simple service access without complex operations.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve</typeparam>
        /// <param name="controller">The controller instance</param>
        /// <returns>The resolved service instance</returns>
        public static TService ResolveService<TService>(this ControllerBase controller)
            where TService : notnull
        {
            return (TService)controller.HttpContext.RequestServices.GetService(typeof(TService))!;
        }

        /// <summary>
        /// Resolves a service on-demand for immediate use in Razor Pages.
        /// Best for: Simple service access without complex operations.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve</typeparam>
        /// <param name="pageModel">The page model instance</param>
        /// <returns>The resolved service instance</returns>
        public static TService ResolveService<TService>(this PageModel pageModel)
            where TService : notnull
        {
            return (TService)pageModel.HttpContext.RequestServices.GetService(typeof(TService))!;
        }

        /// <summary>
        /// Method injection with action execution - no return value.
        /// Best for: Cache operations, logging, void service methods.`
        /// Benefits: Service resolved only for the duration of the action.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <param name="controller">The controller instance</param>
        /// <param name="action">The action to execute with the resolved service</param>
        public static void WithService<TService>(this ControllerBase controller, Action<TService> action)
            where TService : notnull
        {
            var service = controller.ResolveService<TService>();
            action(service);
        }

        /// <summary>
        /// Method injection with action execution - no return value for Razor Pages.
        /// Best for: Cache operations, logging, void service methods.
        /// Benefits: Service resolved only for the duration of the action.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <param name="pageModel">The page model instance</param>
        /// <param name="action">The action to execute with the resolved service</param>
        public static void WithService<TService>(this PageModel pageModel, Action<TService> action)
            where TService : notnull
        {
            var service = pageModel.ResolveService<TService>();
            action(service);
        }

        /// <summary>
        /// Method injection with function execution and return value.
        /// Best for: Data processing, transformations, calculations.
        /// Benefits: Service resolved only when needed, immediate disposal after use.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="controller">The controller instance</param>
        /// <param name="func">The function to execute with the resolved service</param>
        /// <returns>The result of the function execution</returns>
        public static TResult WithService<TService, TResult>(this ControllerBase controller, Func<TService, TResult> func)
            where TService : notnull
        {
            var service = controller.ResolveService<TService>();
            return func(service);
        }

        /// <summary>
        /// Method injection with function execution and return value for Razor Pages.
        /// Best for: Data processing, transformations, calculations.
        /// Benefits: Service resolved only when needed, immediate disposal after use.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="pageModel">The page model instance</param>
        /// <param name="func">The function to execute with the resolved service</param>
        /// <returns>The result of the function execution</returns>
        public static TResult WithService<TService, TResult>(this PageModel pageModel, Func<TService, TResult> func)
            where TService : notnull
        {
            var service = pageModel.ResolveService<TService>();
            return func(service);
        }

        #endregion

        #region Asynchronous Method Injection

        /// <summary>
        /// Asynchronous method injection with async action execution - no return value.
        /// Best for: Async operations like database calls, API requests, file I/O.
        /// Benefits: Non-blocking service resolution and execution.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <param name="controller">The controller instance</param>
        /// <param name="action">The async action to execute with the resolved service</param>
        /// <returns>A task representing the async operation</returns>
        public static async Task WithServiceAsync<TService>(this ControllerBase controller, Func<TService, Task> action)
            where TService : notnull
        {
            var service = controller.ResolveService<TService>();
            await action(service);
        }

        /// <summary>
        /// Asynchronous method injection with async action execution - no return value for Razor Pages.
        /// Best for: Async operations like database calls, API requests, file I/O.
        /// Benefits: Non-blocking service resolution and execution.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <param name="pageModel">The page model instance</param>
        /// <param name="action">The async action to execute with the resolved service</param>
        /// <returns>A task representing the async operation</returns>
        public static async Task WithServiceAsync<TService>(this PageModel pageModel, Func<TService, Task> action)
            where TService : notnull
        {
            var service = pageModel.ResolveService<TService>();
            await action(service);
        }

        /// <summary>
        /// Asynchronous method injection with async function execution and return value.
        /// Best for: Complex async operations that need to return data.
        /// Benefits: Optimal for I/O-bound operations with return values.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="controller">The controller instance</param>
        /// <param name="func">The async function to execute with the resolved service</param>
        /// <returns>A task containing the result of the function execution</returns>
        public static async Task<TResult> WithServiceAsync<TService, TResult>(this ControllerBase controller, Func<TService, Task<TResult>> func)
            where TService : notnull
        {
            var service = controller.ResolveService<TService>();
            return await func(service);
        }

        /// <summary>
        /// Asynchronous method injection with async function execution and return value for Razor Pages.
        /// Best for: Complex async operations that need to return data.
        /// Benefits: Optimal for I/O-bound operations with return values.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="pageModel">The page model instance</param>
        /// <param name="func">The async function to execute with the resolved service</param>
        /// <returns>A task containing the result of the function execution</returns>
        public static async Task<TResult> WithServiceAsync<TService, TResult>(this PageModel pageModel, Func<TService, Task<TResult>> func)
            where TService : notnull
        {
            var service = pageModel.ResolveService<TService>();
            return await func(service);
        }

        #endregion

        #region Conditional Method Injection

        /// <summary>
        /// Conditional method injection - only resolves service if condition is met.
        /// Best for: Optional features, feature flags, performance optimizations.
        /// Benefits: Avoids unnecessary service resolution when conditions aren't met.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <param name="controller">The controller instance</param>
        /// <param name="condition">The condition to check before resolving the service</param>
        /// <param name="action">The action to execute if condition is true</param>
        /// <returns>True if the action was executed, false otherwise</returns>
        public static bool WithServiceIf<TService>(this ControllerBase controller, bool condition, Action<TService> action)
            where TService : notnull
        {
            if (!condition) return false;
            
            controller.WithService(action);
            return true;
        }

        /// <summary>
        /// Conditional method injection - only resolves service if condition is met for Razor Pages.
        /// Best for: Optional features, feature flags, performance optimizations.
        /// Benefits: Avoids unnecessary service resolution when conditions aren't met.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <param name="pageModel">The page model instance</param>
        /// <param name="condition">The condition to check before resolving the service</param>
        /// <param name="action">The action to execute if condition is true</param>
        /// <returns>True if the action was executed, false otherwise</returns>
        public static bool WithServiceIf<TService>(this PageModel pageModel, bool condition, Action<TService> action)
            where TService : notnull
        {
            if (!condition) return false;
            
            pageModel.WithService(action);
            return true;
        }

        /// <summary>
        /// Conditional async method injection with return value.
        /// Best for: Optional async operations that may or may not be needed.
        /// Benefits: Conditional execution with async support and return values.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="controller">The controller instance</param>
        /// <param name="condition">The condition to check before resolving the service</param>
        /// <param name="func">The async function to execute if condition is true</param>
        /// <param name="defaultValue">The default value to return if condition is false</param>
        /// <returns>The result of the function or the default value</returns>
        public static async Task<TResult> WithServiceIfAsync<TService, TResult>(this ControllerBase controller, bool condition, Func<TService, Task<TResult>> func, TResult defaultValue)
            where TService : notnull
        {
            if (!condition) return defaultValue;
            
            return await controller.WithServiceAsync(func);
        }

        /// <summary>
        /// Conditional async method injection with return value for Razor Pages.
        /// Best for: Optional async operations that may or may not be needed.
        /// Benefits: Conditional execution with async support and return values.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve and use</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="pageModel">The page model instance</param>
        /// <param name="condition">The condition to check before resolving the service</param>
        /// <param name="func">The async function to execute if condition is true</param>
        /// <param name="defaultValue">The default value to return if condition is false</param>
        /// <returns>The result of the function or the default value</returns>
        public static async Task<TResult> WithServiceIfAsync<TService, TResult>(this PageModel pageModel, bool condition, Func<TService, Task<TResult>> func, TResult defaultValue)
            where TService : notnull
        {
            if (!condition) return defaultValue;
            
            return await pageModel.WithServiceAsync(func);
        }

        #endregion

        #region Multiple Service Method Injection

        /// <summary>
        /// Resolve multiple services for complex operations.
        /// Best for: Operations requiring multiple coordinated services.
        /// Benefits: Clean syntax for multi-service operations.
        /// </summary>
        /// <typeparam name="TService1">First service type</typeparam>
        /// <typeparam name="TService2">Second service type</typeparam>
        /// <param name="controller">The controller instance</param>
        /// <param name="action">Action to execute with both services</param>
        public static void WithServices<TService1, TService2>(this ControllerBase controller, Action<TService1, TService2> action)
            where TService1 : notnull
            where TService2 : notnull
        {
            var service1 = controller.ResolveService<TService1>();
            var service2 = controller.ResolveService<TService2>();
            action(service1, service2);
        }

        /// <summary>
        /// Resolve multiple services for complex operations in Razor Pages.
        /// Best for: Operations requiring multiple coordinated services.
        /// Benefits: Clean syntax for multi-service operations.
        /// </summary>
        /// <typeparam name="TService1">First service type</typeparam>
        /// <typeparam name="TService2">Second service type</typeparam>
        /// <param name="pageModel">The page model instance</param>
        /// <param name="action">Action to execute with both services</param>
        public static void WithServices<TService1, TService2>(this PageModel pageModel, Action<TService1, TService2> action)
            where TService1 : notnull
            where TService2 : notnull
        {
            var service1 = pageModel.ResolveService<TService1>();
            var service2 = pageModel.ResolveService<TService2>();
            action(service1, service2);
        }

        /// <summary>
        /// Async method injection with multiple services and return value.
        /// Best for: Complex async operations requiring multiple services.
        /// Benefits: Coordinated multi-service async operations.
        /// </summary>
        /// <typeparam name="TService1">First service type</typeparam>
        /// <typeparam name="TService2">Second service type</typeparam>
        /// <typeparam name="TResult">Return type</typeparam>
        /// <param name="controller">The controller instance</param>
        /// <param name="func">Async function to execute with both services</param>
        /// <returns>The result of the async operation</returns>
        public static async Task<TResult> WithServicesAsync<TService1, TService2, TResult>(this ControllerBase controller, Func<TService1, TService2, Task<TResult>> func)
            where TService1 : notnull
            where TService2 : notnull
        {
            var service1 = controller.ResolveService<TService1>();
            var service2 = controller.ResolveService<TService2>();
            return await func(service1, service2);
        }

        /// <summary>
        /// Async method injection with multiple services and return value for Razor Pages.
        /// Best for: Complex async operations requiring multiple services.
        /// Benefits: Coordinated multi-service async operations.
        /// </summary>
        /// <typeparam name="TService1">First service type</typeparam>
        /// <typeparam name="TService2">Second service type</typeparam>
        /// <typeparam name="TResult">Return type</typeparam>
        /// <param name="pageModel">The page model instance</param>
        /// <param name="func">Async function to execute with both services</param>
        /// <returns>The result of the async operation</returns>
        public static async Task<TResult> WithServicesAsync<TService1, TService2, TResult>(this PageModel pageModel, Func<TService1, TService2, Task<TResult>> func)
            where TService1 : notnull
            where TService2 : notnull
        {
            var service1 = pageModel.ResolveService<TService1>();
            var service2 = pageModel.ResolveService<TService2>();
            return await func(service1, service2);
        }

        #endregion

        #region Safe Method Injection with Exception Handling

        /// <summary>
        /// Safe method injection with built-in exception handling.
        /// Best for: Operations where service resolution might fail.
        /// Benefits: Graceful fallback when services are not available.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="controller">The controller instance</param>
        /// <param name="func">Function to execute with the service</param>
        /// <param name="fallback">Fallback function if service resolution fails</param>
        /// <returns>Result of the operation or fallback value</returns>
        public static TResult WithServiceSafe<TService, TResult>(this ControllerBase controller, Func<TService, TResult> func, Func<TResult> fallback)
            where TService : notnull
        {
            try
            {
                var service = controller.ResolveService<TService>();
                return func(service);
            }
            catch
            {
                return fallback();
            }
        }

        /// <summary>
        /// Safe method injection with built-in exception handling for Razor Pages.
        /// Best for: Operations where service resolution might fail.
        /// Benefits: Graceful fallback when services are not available.
        /// </summary>
        /// <typeparam name="TService">The service type to resolve</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="pageModel">The page model instance</param>
        /// <param name="func">Function to execute with the service</param>
        /// <param name="fallback">Fallback function if service resolution fails</param>
        /// <returns>Result of the operation or fallback value</returns>
        public static TResult WithServiceSafe<TService, TResult>(this PageModel pageModel, Func<TService, TResult> func, Func<TResult> fallback)
            where TService : notnull
        {
            try
            {
                var service = pageModel.ResolveService<TService>();
                return func(service);
            }
            catch
            {
                return fallback();
            }
        }

        #endregion
    }
}