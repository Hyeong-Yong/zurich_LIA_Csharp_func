# zurich_LIA_Csharp_func
Example code : C# function 

String serverVersion = daq.getByte("/zi/about/version");

daq.setInt(String.Format("/{0}/system/preset/load", dev), 1);

daq.setDouble(String.Format("/{0}/demods/{1}/timeconstant", dev, demod_c), time_constant);

String apiVersion = daq.version();

String id = daq.discoveryFind(dev);

String iface = daq.discoveryGetValueS(dev, "connected"); // String 값 반환

long port = daq.discoveryGetValueI(dev, "serverport"); // long 값 반환

daq.init(host, port, api);

daq.connectDevice(dev, iface, "");

daq.subscribe(path);

daq.poll(duration, timeOutMilliseconds, flags, bufferSize)
ex) daq.poll(0.1, 100, 0, 1);
