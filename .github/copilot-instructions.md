# .NET Performance 101: Copilot Instructions

When assisting with code in this workspace, please adhere to the following .NET performance best practices. Prioritize these principles when generating, refactoring, or optimizing code.

## Performance Guidelines

**1. Caching:**
   - Utilize in-memory caching (`IMemoryCache`) for frequently accessed data that is not user-specific.
   - For distributed scenarios, consider using a distributed cache like Redis.

**2. Database Optimization:**
   - Write efficient database queries.
   - Ensure proper indexing on tables, especially for columns used in `WHERE` clauses and `JOIN` operations.
   - Use connection pooling to minimize the overhead of opening and closing database connections.

**3. Asynchronous Programming:**
   - Use `async` and `await` for all I/O-bound and CPU-intensive operations to keep the application responsive. This includes database calls, file system access, and external API requests.

**4. Entity Framework Core Usage:**
   - Use eager loading (`Include`) to avoid N+1 query problems.
   - Use projections (`Select`) to retrieve only the data you need.
   - For highly-trafficked queries, consider using compiled queries for better performance.

**5. Memory Management:**
   - Prefer `struct`s (value types) for small, short-lived objects.
   - Be mindful of large object graphs to avoid excessive memory consumption.
   - Implement the `IDisposable` pattern and use `using` statements for unmanaged resources like database connections and streams.
   - Avoid boxing and unboxing by using generics or appropriate type handling.
   - Use `StringBuilder` for concatenating strings in loops or for a large number of appends.

**6. HTTP Caching:**
   - Implement HTTP caching strategies using headers like `ETag` and `Last-Modified` to reduce unnecessary data transfer.

**7. Minimize Round-Trips:**
   - Reduce the number of HTTP requests by bundling assets or using techniques like server-side rendering.
   - Minimize database round-trips by fetching all required data in a single query where possible.

**8. Content Delivery Networks (CDNs):**
   - Offload static assets (CSS, JavaScript, images) to a CDN for faster delivery to users across different geographical locations.

**9. Compression:**
   - Enable GZIP or Brotli compression for HTTP responses to reduce the size of data transferred over the network.

**10. Logging and Tracing:**
    - Avoid excessive or verbose logging in production environments to minimize performance overhead.
    - Use distributed tracing to monitor requests across microservices and identify bottlenecks.

**11. Parallelism and Concurrency:**
    - Utilize the Task Parallel Library (TPL) or `Parallel.ForEach` for CPU-bound tasks that can be executed in parallel.

**12. Resource Optimization:**
    - Optimize images and other static assets for the web to reduce their file size and improve load times.

**13. HTTP/2:**
    - Enable HTTP/2 over SSL/TLS to leverage its performance benefits, such as multiplexing and server push.

**14. Measure and Monitor Performance:**
    - Before optimizing, always measure performance to identify bottlenecks.
    - Use tools like Visual Studio Diagnostic Tools, Application Insights, or BenchmarkDotNet to profile and monitor application performance.

**15. Use `Span<T>`:**
    - For performance-critical code paths involving array or buffer manipulation, use `Span<T>` and `Memory<T>` to avoid unnecessary allocations and copies.

---

**Security Guidelines:**

- Never put untrusted data into your HTML input, unless you follow the rest of the steps below. Untrusted data is any data that may be controlled by a cyberattacker, such as HTML form inputs, query strings, HTTP headers, or even data sourced from a database, as a cyberattacker may be able to breach your database even if they can't breach your application.
- Before putting untrusted data into an HTML element, ensure that it's HTML encoded. HTML encoding takes characters such as `<` and changes them into a safe form like `&lt;`.
- Before putting untrusted data into an HTML attribute, ensure that it's HTML attribute encoded. This specialized form of HTML encoding handles double quotes (`"`), single quotes (`'`), ampersands (`&`), and less-than (`<`) characters. When dealing with untrusted input, use HTML encoding for general HTML content and HTML attribute encoding for HTML attributes.
- Before putting untrusted data into JavaScript, place the data in an HTML element whose contents you retrieve at runtime. If this isn't possible, then ensure the data is JavaScript encoded. JavaScript encoding takes dangerous characters for JavaScript and replaces them with their hex, for example, `<` would be encoded as `\u003C`.
- Before putting untrusted data into a URL query string ensure it's URL encoded.

---

**Golden Rule:** Always analyze and measure performance before and after making improvements to validate their impact.
