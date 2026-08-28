#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LoggingFactoryRegistry](Compono.Logging.LoggingFactoryRegistry.md 'Compono\.Logging\.LoggingFactoryRegistry')

## LoggingFactoryRegistry\.Register\<TCategory\>\(Func\<LoggingOptions,object\>\) Method

Idempotently registers [factory](Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).md#Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).factory 'Compono\.Logging\.LoggingFactoryRegistry\.Register\<TCategory\>\(System\.Func\<Compono\.Logging\.LoggingOptions,object\>\)\.factory') as the activator for
`ILogger<>`\. [TCategory](Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).md#Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).TCategory 'Compono\.Logging\.LoggingFactoryRegistry\.Register\<TCategory\>\(System\.Func\<Compono\.Logging\.LoggingOptions,object\>\)\.TCategory') is
closed statically wherever this is called from generated code \- `typeof(ILogger{TCategory})`
here is an ordinary generic\-token load inside this method's own per\-[TCategory](Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).md#Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).TCategory 'Compono\.Logging\.LoggingFactoryRegistry\.Register\<TCategory\>\(System\.Func\<Compono\.Logging\.LoggingOptions,object\>\)\.TCategory')
compiled instantiation, never [System\.Type\.MakeGenericType\(System\.Type\[\]\)](https://learn.microsoft.com/en-us/dotnet/api/system.type.makegenerictype#system-type-makegenerictype(system-type[]) 'System\.Type\.MakeGenericType\(System\.Type\[\]\)')\.

```csharp
public static void Register<TCategory>(System.Func<Compono.Logging.LoggingOptions,object> factory);
```
#### Type parameters

<a name='Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).TCategory'></a>

`TCategory`
#### Parameters

<a name='Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).factory'></a>

`factory` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[LoggingOptions](Compono.Logging.LoggingOptions.md 'Compono\.Logging\.LoggingOptions')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')