ApplicationMetrics
---
ApplicationMetrics provides simple interfaces and classes to allow capturing metric and instrumentation information from a client application.  It was designed with the following goals...

1. Provide interfaces which can be injected into client classes, and provide simple methods for logging metrics from these classes.
2. To ensure that details of how the metrics are stored and displayed is hidden behind the interfaces
3. To provide a simple mechanism of viewing metrics through the Windows Performance Monitor (i.e. simpler than that provided by the .NET PerformanceCounter and CounterCreationData classes)\*.
4. To provide additional implementation of metric loggers and viewers for files, console, and relational databases, plus base classes to allow consumers to easily provide their own implementations of metric loggers and viewers†.

\* Note that the PerformanceCounterMetricLogger class which was used to view metrics through the Windows Performance Monitor, has been moved to a [separate project](https://github.com/alastairwyse/ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter) since this project was migrated to .NET Standard.  
† The [MetricLoggers.SqlServer](https://github.com/alastairwyse/ApplicationMetrics.MetricLoggers.SqlServer) amd [MetricLoggers.PostgreSql](https://github.com/alastairwyse/ApplicationMetrics.MetricLoggers.PostgreSql) projects serve as examples of implementing metric loggers which write to relational databases

#### Getting Started

##### 1) Defining Metrics
Metrics are defined, by deriving from the CountMetric, AmountMetric, StatusMetric, and IntervalMetric classes.  The difference between these metric types is outlined below...

<table>
  <tr>
    <td><b>Class</b></td>
    <td><b>Description</b></td>
  </tr>
  <tr>
    <td valign="top">CountMetric</td>
    <td>Used when you need to record the number of instances of an event and where the events increments by 1 each time (e.g. number of disk read operations, number of messages sent to a remote system, number of cache hits)</td>
  </tr>
  <tr>
    <td valign="top">AmountMetric</td>
    <td>Used to record events which have an associated size which accumulates, but not necessarily by 1 each time (e.g. the size in bytes of a message sent to a remote system)</td>
  </tr>
  <tr>
    <td valign="top">StatusMetric</td>
    <td>Used to record events which have an associated size which varies over time rather than accumulating (e.g. total amount of free memory).  The distinction from AmountMetrics is that summing the total recorded amounts over successive AmountMetric events has meaning (e.g. the total number of bytes sent to a remote system totalled across multiple sent messages), whereas summing the total recorded amounts over successive StatusMetrics would not (e.g. summed free memory across multiple sampling points).</td>
  </tr>
  <tr>
    <td valign="top">IntervalMetric</td>
    <td>Used to record the time taken for an event to complete (e.g. total time taken to send a message to a remote system).  This is calculated by capturing the start and end times of an IntervalMetric event.  The default implementation captures IntervalMetrics in milliseconds.</td>
  </tr>
</table>

In this sample case ApplicationMetrics is used to capture instrumentation from a class which sends a message to a remote location.  We would define the following 3 metrics...

````C#
class MessageSent : CountMetric
{
    public MessageSent()
    {
        base.name = "MessageSent";
        base.description = "A message was sent";
    }
}

class MessageSize : AmountMetric
{
    public MessageSize()
    {
        base.name = "MessageSize";
        base.description = "The size of a sent message";
    }
}

class MessageSendTime : IntervalMetric
{
    public MessageSendTime()
    {
        base.name = "MessageSendTime";
        base.description = "The time taken to send a message";
    }
}
````

##### 2) Using the IMetricLogger interface
The IMetricLogger interface should be injected into the client class.  The example below shows our message sending class, with an instance of IMetricLogger used to log the above metrics when a message is sent.

````C#
public class MessageSender
{
    private IMetricLogger metricLogger;

    public MessageSender(IMetricLogger metricLogger)
    {
        this.metricLogger = metricLogger;
    }

    public void Send(String message)
    {
        Guid beginId = metricLogger.Begin(new MessageSendTime());

        // Call private method to perform the send
        try
        {
            SendMessage(message);
        }
        catch
        {
            metricLogger.CancelBegin(beginId, new MessageSendTime());
            throw;
        }

        metricLogger.End(beginId, new MessageSendTime());
        metricLogger.Increment(new MessageSent());
        metricLogger.Add(new MessageSize(), message.Length);
    }
````

The MessageSender class could be instantiated using a FileMetricLogger with the below statements...

````C#
FileMetricLogger metricLogger  = new FileMetricLogger('|', @"C:\Test\MessageSenderMetrics.log", new LoopingWorkerThreadBufferProcessor(1000), IntervalMetricBaseTimeUnit.Millisecond, true);
MessageSender testMessageSender = new MessageSender(metricLogger);
````

##### 3) Choosing a Buffer Processing Strategy
Metric logger classes implement IMetricLogger and generally derive from class MetricLoggerBuffer.  MetricLoggerBuffer buffers any logged metrics internally and persists the metrics periodically.  Classes implementing IBufferProcessingStrategy are passed to the MetricLoggerBuffer constructor, and determine when the buffers should be processed and the metrics persisted.  3 implementations of IBufferProcessingStrategy are included...

<table>
  <tr>
    <td><b>Class</b></td>
    <td><b>Description</b></td>
  </tr>
  <tr>
    <td valign="top">LoopingWorkerThreadBufferProcessor</td>
    <td>Processes the buffers at a specified interval in a loop</td>
  </tr>
  <tr>
    <td valign="top">SizeLimitedBufferProcessor</td>
    <td>Processes the buffers when the total number of buffered metrics reaches a specified count.</td>
  </tr>
  <tr>
    <td valign="top">SizeLimitedLoopingWorkerThreadHybridBufferProcessor</td>
    <td>Processes the buffers when either a specified loop interval elapses, or when the total number of buffered metrics reaches a specified count, whichever occurs first.</td>
  </tr>
</table>

##### 4) Using the IMetricAggregateLogger interface
Classes that implement IMetricAggregateLogger (ConsoleMetricLogger and PerformanceCounterMetricLogger) let you define and log aggregates of individual metrics.  The example client code below shows how to define some aggregates for the above metrics...

````C#
static void Main(string[] args)
{
    LoopingWorkerThreadBufferProcessor bufferProcessor = new LoopingWorkerThreadBufferProcessor(5000);
    ConsoleMetricLogger metricLogger = new ConsoleMetricLogger(bufferProcessor, IntervalMetricBaseTimeUnit.Millisecond, true);

    // Define a metric aggregate to record the average size of sent messages (total message size / number of messages sent)
    metricLogger.DefineMetricAggregate(new MessageSize(), new MessageSent(), "AverageMessageSize", "The average size of sent messages");

    // Define a metric aggregate to record the number of messages sent per second (number of messages sent / number of seconds of runtime)
    metricLogger.DefineMetricAggregate(new MessageSent(), TimeUnit.Second, "MessagesSentPerSecond", "The number of messages sent per second");
}
````

##### 5) Viewing the metrics
When started, the ConsoleMetricLogger will produce output similar to the following...

```
---------------------------------------------------
-- Application metrics as of 2015-06-16 13:01:11 --
---------------------------------------------------
MessageSent: 207
MessageSize: 1223510
MessageSendTime: 12834
AverageMessageSize: 5910.676328502415
MessagesSentPerSecond: 2.41545893719806
```

##### 6) Filtering metrics
Metric logger filters have been included since version 6.0.0 (in the ApplicationMetrics.Filters namespace).  These implement filtering by wrapping metric logger instances, and can be 'chained' together in sequence following a [decorator](https://en.wikipedia.org/wiki/Decorator_pattern)-type pattern.  See the below example...

````C#
    // Create a ConsoleMetricLogger
    LoopingWorkerThreadBufferProcessor bufferProcessor = new LoopingWorkerThreadBufferProcessor(3000);
    var consoleMetricLogger = new ConsoleMetricLogger(bufferProcessor, IntervalMetricBaseTimeUnit.Millisecond, true);
    // Create an exclusion filter which excludes/removes 'MessageSent' count metrics
    var exclusionFilter = new MetricLoggerExclusionFilter
    (
        consoleMetricLogger, 
        new List<CountMetric>() { new MessageSent() }, 
        new List<AmountMetric>(), 
        new List<StatusMetric>(), 
        new List<IntervalMetric>()
    );
    // Create a type filter which only logs count metrics
    //   2 filters are now 'chained' before the console metric logger i.e. typeFilter > exclusionFilter > consoleMetricLogger
    var typeFilter = new MetricLoggerTypeFilter(exclusionFilter, true, false, false, false);

    consoleMetricLogger.Start();

    // 'DiskReadOperation' count metrics will be logged
    typeFilter.Increment(new DiskReadOperation());
    // 'MessageSent' count metrics are excluded by the exclusion filter, hence the below is not logged
    typeFilter.Increment(new MessageSent());
    // Amount metrics are excluded by the type filter, so the below 'MessageSize' metric is also not logged
    typeFilter.Add(new MessageSize(), 2048);
````

#### 'Interleaved' Interval Metrics
Since version 5.0.0 the MetricLoggerBuffer class (and its subclasses) supports 'interleaving' of interval metrics... i.e. allowing multiple interval metrics of the same type to be in a begun/started state at the same time (as would occur if methods Begin() &gt; Begin() &gt; End() &gt; End() were called in sequence for the same interval metric).  This is especially important when the client application is logging metrics from multiple threads.  This is facilitated by the Begin() method returning a unique Guid, which should subsequently be passed to the matching End() or CancelBegin() method.  For backwards (i.e. prior to version 5.0.0) compatibility, the former versions of the End() and CancelBegin() methods which *don't* accept a Guid are still maintained, as is the 'intervalMetricChecking' constructor parameter (which worked around the issue of not supporting interleaving by not throwing exceptions if interleaved interval metrics logging calls were received).  Depending on whether the first call to the End() or CancelBegin() methods includes the Guid parameter or not, the interleaving mode is selected accordingly... i.e. either interleaved (newer, including the Guid as a parameter) or non-interleaved (older, omitting the Guid).  Once the mode is set, calling method overloads corresponding to the other mode will throw an exception, so client code must consistently call either the Guid or non-Guid overloads of these methods.  

Non-interleaved mode may be deprecated in future versions, so it is recommended to migrate client code to support interleaved mode.

#### Exception Handling
Classes implementing IBufferProcessingStrategy typically perform the processing/persisting of buffered metrics using a dedicated worker thread.  Since this processing often involves writing to remote persistant storage, there is a risk of unexpected transient errors.  Implementations of IBufferProcessingStrategy expose several optional constructor parameters to control the behaviour when encountering such errors/exceptions...

<table>
  <tr>
    <td><b>Parameter</b></td>
    <td><b>Description</b></td>
  </tr>
  <tr>
    <td valign="top">bufferProcessingExceptionAction</td>
    <td>An Action which is invoked when an error/exception is encountered, and which accepts the exception as a parameter.  Can be used (for example) to log or notify another system of the error, or to adjust the client application to handle or work around the error.</td>
  </tr>
  <tr>
    <td valign="top">rethrowBufferProcessingException</td>
    <td>
      If set true, any error/exception encountered will be rethrown on the client application thread on the next call to the IMetricLogger Increment(), Add(), Set(), Begin(), End(), or CancelBegin() methods.  If set false an error/exception will not be rethrown.  Defaults to true if not specified.  Regardless of the setting, the Action set on parameter 'bufferProcessingExceptionAction' will be invoked with the encountered exception.<br /><br />  
      Note that when an unexpected exception is encountered, the buffer processing worker thread will be terminated, if 'rethrowBufferProcessingException' is set false and metrics continue to be logged, the buffers will eventually fill up and result in out of memory exceptions.  If 'rethrowBufferProcessingException' is set false, the client application should stop logging further metrics when a buffer processing exception occurs.
    </td>
  </tr>
</table>

#### Links
The documentation below was written for version 1.* of ApplicationMetrics.  Minor implementation details may have changed in versions 2.0.0 and above, however the basic principles and use cases documented are still valid.  Note also that this documentation demonstrates the older 'non-interleaved' method of logging interval metrics.

Full documentation for the project...<br>
[http://www.alastairwyse.net/methodinvocationremoting/application-metrics.html](http://www.alastairwyse.net/methodinvocationremoting/application-metrics.html)

A detailed sample implementation...<br>
[http://www.alastairwyse.net/methodinvocationremoting/sample-application-5.html](http://www.alastairwyse.net/methodinvocationremoting/sample-application-5.html)

#### Release History

<table>
  <tr>
    <td><b>Version</b></td>
    <td><b>Changes</b></td>
  </tr>
  <tr>
    <td valign="top">7.0.0</td>
    <td>  
      Corrected 'Operations that change non-concurrent collections must have exclusive access' exception when calling methods on the MetricLoggerBase class concurrently from multiple threads.
    </td>
  </tr>
  <tr>
    <td valign="top">6.4.1</td>
    <td>  
      Further refactoring to allow introducing MetricLogger implementations which adapt to other metric frameworks.
    </td>
  </tr>
  <tr>
    <td valign="top">6.4.0</td>
    <td>  
      Added abstract class MetricLoggerBase to allow introducing MetricLogger implementations which adapt to other metric frameworks.
    </td>
  </tr>
  <tr>
    <td valign="top">6.3.0</td>
    <td>  
      Added parameters to buffer processing classes to invoke a specified lambda on buffer processing failure, and to determine whether the exception causing the failure should be rethrown on the main metric logger classes' thread.
    </td>
  </tr>
  <tr>
    <td valign="top">6.2.0</td>
    <td>  
      Corrected interval metric calculation when Stopwatch.Frequency property != 10,000,000.
    </td>
  </tr>
  <tr>
    <td valign="top">6.1.0</td>
    <td>  
      Fixed Int64 overflow bug when logging interval metrics in MetricLoggerBuffer due to unnecessary internal conversion to Double.
    </td>
  </tr>
  <tr>
    <td valign="top">6.0.0</td>
    <td>  
      Added constructor parameter 'intervalMetricBaseTimeUnit' metric logger constructors.  Allows interval metrics to be logged in either milliseconds (former default) or nanoseconds (new option), to more accurately log interval metrics for high performance client applications.<br />
      Added SizeLimitedLoopingWorkerThreadHybridBufferProcessor class... an implementation of IBufferProcessingStrategy which processes the buffers when either the total number of buffered metric events reaches a pre-defined limit or a specified looping interval expires, whichever occurs first.<br />
      Added 'Filters' project/namespace, and 3 filter implementations (MetricLoggerExclusionFilter, MetricLoggerInclusionFilter, MetricLoggerTypeFilter) which can be used in a decorator pattern in front of metric loggers.
    </td>
  </tr>
  <tr>
    <td valign="top">5.1.0</td>
    <td>  
      Updated the WorkerThreadBufferProcessorBase class to use ExceptionDispatchInfo to rethrow exceptions from the worker thread to the main thread, to better preserve the state and context of when the exception occurred.
    </td>
  </tr>
  <tr>
    <td valign="top">5.0.0</td>
    <td>  
      Added support for 'interleaved' interval metrics.
    </td>
  </tr>
  <tr>
    <td valign="top">4.0.0</td>
    <td>  
      Moved the 'Amount' property/parameter on the AmountMetric class and the 'Value' property/parameter on the StatusMetric class to be parameters of the Add() and Set() methods (respectively) on the IMetricLogger interface.  This is a fairly fundamental breaking change, but necessary as it creates a clear separation between the definition of the metrics (in subclasses of AmountMetric and StatusMetric),  and instances of the metrics (created via calls to IMetricLogger, and stored in instances of classes AmountMetricEventInstance and StatusMetricEventInstance).
    </td>
  </tr>
  <tr>
    <td valign="top">3.0.0</td>
    <td>
      MetricLoggerBuffer Process*MetricEvents() methods signatures changed to pass a collection of metric event instances (rather than single metric event instance) to allow bulk transfer to external systems and/or storage.<br />
      MetricLoggerBuffer DequeueAndProcess*MetricEvents() methods updated to move events to temporary queue via reference swap rather than moving of individual items for better performance.<br />
      WorkerThreadBufferProcessorBase Notify*MetricEventBuffered() and Notify*MetricEventBufferCleared() methods use the Interlocked class to update queue item counts to prevent compiler instruction reordering.<br />
      Worker thread exception handling re-written to properly catch and re-throw exceptions on subsequent calls to main thread methods.<br />
      Removed *LoggerImplementation classes.<br />
      MetricAggregateContainer* classes made nested classes of MetricAggregateLogger.<br />
      MetricEventInstance* classes made nested classes of MetricLoggerBuffer.<br />
      MetricTotalContainer* classes made nested of MetricLoggerStorer.<br />
      IMetricLogger interface moved to ApplicationMetrics namespace.<br />
      General re-write of and improvement to unit tests.<br />
    </td>
  </tr>
  <tr>
    <td valign="top">2.0.0</td>
    <td>
      Migrated to .NET Standard.<br />
      Removed MicrosoftAccessMetricLogger class.<br />
      PerformanceCounterMetricLogger class moved to separate ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter project.<br />
      MetricLoggerBuffer enhanced to use Stopwatch class for greater accuracy of metrics.<br />
      Change to LoopingWorkerThreadBufferProcessor class constructor parameters.<br />
    </td>
  </tr>
  <tr>
    <td valign="top">1.5.0.0</td>
    <td>
      MetricLoggerBuffer class updated to implement IDisposable, and unhook buffer processed event handlers on dispose.<br />
      Classes deriving from MetricLoggerBuffer updated to implement IDisposable.
    </td>
  </tr>
  <tr>
    <td valign="top">1.4.0.0</td>
    <td>
      Initial version forked from the <a href="http://www.alastairwyse.net/methodinvocationremoting/">Method Invocation Remoting</a> project.
    </td>
  </tr>
</table>