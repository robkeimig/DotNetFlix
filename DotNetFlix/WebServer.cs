using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Features;
using System.Data.SQLite;
using DotNetFlix.Data;
using DotNetFlix.Pages;
using System.Text;

namespace DotNetFlix;

internal class WebServer
{
    readonly SQLiteConnection Sql;

    public WebServer(SQLiteConnection sql)
    {
        Sql = sql;

        WebHost.CreateDefaultBuilder()
            .UseKestrel(k =>
            {
                k.ListenAnyIP(80);
            })
            .UseStartup<WebStartup>()
            .ConfigureLogging(cl =>
            {
                cl.ClearProviders();
            })
            .ConfigureServices(s =>
            {
                s.AddSingleton(this);
                s.Configure<FormOptions>(options =>
                {
                    options.MultipartBodyLengthLimit = Constants.MaximumUploadSize;
                });
            })
            .ConfigureKestrel(s =>
            {
                s.Limits.MaxRequestBodySize = Constants.MaximumUploadSize;
            })
            .Build()
            .RunAsync();
    }

    internal async Task ProcessHttpContext(HttpContext context)
    {
        Console.WriteLine(context.Request.Path);

        if (context.Request.Path.StartsWithSegments("/upnp/Description.xml"))
        {
            context.Response.ContentType = "application/xml";
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(UpnpDescriptionXml));
            return;
        }

        if (context.Request.Path.StartsWithSegments("/upnp/ContentDirectory.xml"))
        {
            context.Response.ContentType = "application/xml";
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(UpnpContentDirectoryXml));
            return;
        }

        if (context.Request.Path.StartsWithSegments("/upnp/ConnectionManager.xml"))
        {
            context.Response.ContentType = "application/xml";
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(UpnpConnectionManagerXml));
            return;
        }

        if (context.Request.Path.StartsWithSegments("/upnp/MSMediaReceiverRegistrar.xml"))
        {
            context.Response.ContentType = "application/xml";
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(UpnpMSMediaReceiverRegistrarXml));
            return;
        }

        var cookie = context.GetSessionToken();
        var session = Sql.GetSession(cookie);

        if (session == null)
        {
            session = Sql.CreateSession();
            context.SetSessionToken(session.Token);
            session.Page = nameof(Home);
            Sql.SetSessionPage(session.Id, session.Page);
        }

        var page = Page.Instance(session.Page);

        if (context.Request.Method.Equals("post", StringComparison.CurrentCultureIgnoreCase))
        {
            await page.Post(context, Sql, session.Id);
        }
        else
        {
            await page.Get(context, Sql, session.Id);
        }
    }

    private string UpnpDescriptionXml => @$"
<root xmlns=""urn:schemas-upnp-org:device-1-0"" xmlns:dlna=""urn:schemas-dlna-org:device-1-0""
    xmlns:sec=""http://www.sec.co.kr/dlna"">
    <specVersion>
        <major>1</major>
        <minor>0</minor>
    </specVersion>
    <device>
        <dlna:X_DLNACAP />
        <dlna:X_DLNADOC>DMS-1.50</dlna:X_DLNADOC>
        <UDN>uuid:{UpnpServer.UUID}</UDN>
        <dlna:X_DLNADOC>M-DMS-1.50</dlna:X_DLNADOC>
        <friendlyName>DotNetFlix</friendlyName>
        <deviceType>urn:schemas-upnp-org:device:MediaServer:1</deviceType>
        <manufacturer>tn123.org</manufacturer>
        <manufacturerURL>https://tn123.org/</manufacturerURL>
        <modelName>sdlna Media Server</modelName>
        <modelDescription />
        <modelNumber>1.0.5406.40050</modelNumber>
        <modelURL>https://tn123.org/</modelURL>
        <serialNumber />
        <sec:ProductCap>smi,DCM10,getMediaInfo.sec,getCaptionInfo.sec</sec:ProductCap>
        <sec:X_ProductCap>smi,DCM10,getMediaInfo.sec,getCaptionInfo.sec</sec:X_ProductCap>
        <iconList>
            <icon>
                <mimetype>image/jpeg</mimetype>
                <width>48</width>
                <height>48</height>
                <depth>24</depth>
                <url>/icon/smallJPEG</url>
            </icon>
            <icon>
                <mimetype>image/png</mimetype>
                <width>48</width>
                <height>48</height>
                <depth>24</depth>
                <url>/icon/smallPNG</url>
            </icon>
            <icon>
                <mimetype>image/png</mimetype>
                <width>120</width>
                <height>120</height>
                <depth>24</depth>
                <url>/icon/largePNG</url>
            </icon>
            <icon>
                <mimetype>image/jpeg</mimetype>
                <width>120</width>
                <height>120</height>
                <depth>24</depth>
                <url>/icon/largeJPEG</url>
            </icon>
        </iconList>
        <serviceList>
            <service>
                <serviceType>urn:schemas-upnp-org:service:ContentDirectory:1</serviceType>
                <serviceId>urn:upnp-org:serviceId:ContentDirectory</serviceId>
                <SCPDURL>/upnp/ContentDirectory.xml</SCPDURL>
                <controlURL>/upnp/control</controlURL>
                <eventSubURL>/upnp/events</eventSubURL>
            </service>
            <service>
                <serviceType>urn:schemas-upnp-org:service:ConnectionManager:1</serviceType>
                <serviceId>urn:upnp-org:serviceId:ConnectionManager</serviceId>
                <SCPDURL>/upnp/ConnectionManager.xml</SCPDURL>
                <controlURL>/upnp/control</controlURL>
                <eventSubURL>/upnp/events</eventSubURL>
            </service>
            <service>
                <serviceType>urn:schemas-upnp-org:service:X_MS_MediaReceiverRegistrar:1</serviceType>
                <serviceId>urn:microsoft.com:serviceId:X_MS_MediaReceiverRegistrar</serviceId>
                <SCPDURL>/upnp/MSMediaReceiverRegistrar.xml</SCPDURL>
                <controlURL>/upnp/control</controlURL>
                <eventSubURL>/upnp/events</eventSubURL>
            </service>
        </serviceList>
    </device>
</root>";

    string UpnpContentDirectoryXml => $@"
<scpd xmlns=""urn:schemas-upnp-org:service-1-0"">
    <specVersion>
        <major>1</major>
        <minor>0</minor>
    </specVersion>
    <actionList>
        <action>
            <name>GetSystemUpdateID</name>
            <argumentList>
                <argument>
                    <name>Id</name>
                    <direction>out</direction>
                    <relatedStateVariable>SystemUpdateID</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
        <action>
            <name>GetSearchCapabilities</name>
            <argumentList>
                <argument>
                    <name>SearchCaps</name>
                    <direction>out</direction>
                    <relatedStateVariable>SearchCapabilities</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
        <action>
            <name>GetSortCapabilities</name>
            <argumentList>
                <argument>
                    <name>SortCaps</name>
                    <direction>out</direction>
                    <relatedStateVariable>SortCapabilities</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
        <action>
            <name>Browse</name>
            <argumentList>
                <argument>
                    <name>ObjectID</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_ObjectID</relatedStateVariable>
                </argument>
                <argument>
                    <name>BrowseFlag</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_BrowseFlag</relatedStateVariable>
                </argument>
                <argument>
                    <name>Filter</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_Filter</relatedStateVariable>
                </argument>
                <argument>
                    <name>StartingIndex</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_Index</relatedStateVariable>
                </argument>
                <argument>
                    <name>RequestedCount</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_Count</relatedStateVariable>
                </argument>
                <argument>
                    <name>SortCriteria</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_SortCriteria</relatedStateVariable>
                </argument>
                <argument>
                    <name>Result</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_Result</relatedStateVariable>
                </argument>
                <argument>
                    <name>NumberReturned</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_Count</relatedStateVariable>
                </argument>
                <argument>
                    <name>TotalMatches</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_Count</relatedStateVariable>
                </argument>
                <argument>
                    <name>UpdateID</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_UpdateID</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
        <action>
            <name>X_GetFeatureList</name>
            <argumentList>
                <argument>
                    <name>FeatureList</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_Featurelist</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
        <action>
            <name>X_SetBookmark</name>
            <argumentList>
                <argument>
                    <name>CategoryType</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_CategoryType</relatedStateVariable>
                </argument>
                <argument>
                    <name>RID</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_RID</relatedStateVariable>
                </argument>
                <argument>
                    <name>ObjectID</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_ObjectID</relatedStateVariable>
                </argument>
                <argument>
                    <name>PosSecond</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_PosSec</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
    </actionList>
    <serviceStateTable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_SortCriteria</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_UpdateID</name>
            <dataType>ui4</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_SearchCriteria</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_Filter</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_Result</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_Index</name>
            <dataType>ui4</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_ObjectID</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>SortCapabilities</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>SearchCapabilities</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_Count</name>
            <dataType>ui4</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_BrowseFlag</name>
            <dataType>string</dataType>
            <allowedValueList>
                <allowedValue>BrowseMetadata</allowedValue>
                <allowedValue>BrowseDirectChildren</allowedValue>
            </allowedValueList>
        </stateVariable>
        <stateVariable sendEvents=""yes"">
            <name>SystemUpdateID</name>
            <dataType>ui4</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_BrowseLetter</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_CategoryType</name>
            <dataType>ui4</dataType>
            <defaultValue />
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_RID</name>
            <dataType>ui4</dataType>
            <defaultValue />
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_PosSec</name>
            <dataType>ui4</dataType>
            <defaultValue />
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_Featurelist</name>
            <dataType>string</dataType>
            <defaultValue />
        </stateVariable>
    </serviceStateTable>
</scpd>
";

    string UpnpConnectionManagerXml => $@"
<scpd xmlns=""urn:schemas-upnp-org:service-1-0"">
    <specVersion>
        <major>1</major>
        <minor>0</minor>
    </specVersion>
    <actionList>
        <action>
            <name>GetCurrentConnectionInfo</name>
            <argumentList>
                <argument>
                    <name>ConnectionID</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_ConnectionID</relatedStateVariable>
                </argument>
                <argument>
                    <name>RcsID</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_RcsID</relatedStateVariable>
                </argument>
                <argument>
                    <name>AVTransportID</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_AVTransportID</relatedStateVariable>
                </argument>
                <argument>
                    <name>ProtocolInfo</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_ProtocolInfo</relatedStateVariable>
                </argument>
                <argument>
                    <name>PeerConnectionManager</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_ConnectionManager</relatedStateVariable>
                </argument>
                <argument>
                    <name>PeerConnectionID</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_ConnectionID</relatedStateVariable>
                </argument>
                <argument>
                    <name>Direction</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_Direction</relatedStateVariable>
                </argument>
                <argument>
                    <name>Status</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_ConnectionStatus</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
        <action>
            <name>GetProtocolInfo</name>
            <argumentList>
                <argument>
                    <name>Source</name>
                    <direction>out</direction>
                    <relatedStateVariable>SourceProtocolInfo</relatedStateVariable>
                </argument>
                <argument>
                    <name>Sink</name>
                    <direction>out</direction>
                    <relatedStateVariable>SinkProtocolInfo</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
        <action>
            <name>GetCurrentConnectionIDs</name>
            <argumentList>
                <argument>
                    <name>ConnectionIDs</name>
                    <direction>out</direction>
                    <relatedStateVariable>CurrentConnectionIDs</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
    </actionList>
    <serviceStateTable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_ProtocolInfo</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_ConnectionStatus</name>
            <dataType>string</dataType>
            <allowedValueList>
                <allowedValue>OK</allowedValue>
                <allowedValue>ContentFormatMismatch</allowedValue>
                <allowedValue>InsufficientBandwidth</allowedValue>
                <allowedValue>UnreliableChannel</allowedValue>
                <allowedValue>Unknown</allowedValue>
            </allowedValueList>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_AVTransportID</name>
            <dataType>i4</dataType>
            <defaultValue>0</defaultValue>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_RcsID</name>
            <dataType>i4</dataType>
            <defaultValue>0</defaultValue>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_ConnectionID</name>
            <dataType>i4</dataType>
            <defaultValue>0</defaultValue>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_ConnectionManager</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""yes"">
            <name>SourceProtocolInfo</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""yes"">
            <name>SinkProtocolInfo</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_Direction</name>
            <dataType>string</dataType>
            <allowedValueList>
                <allowedValue>Input</allowedValue>
                <allowedValue>Output</allowedValue>
            </allowedValueList>
        </stateVariable>
        <stateVariable sendEvents=""yes"">
            <name>CurrentConnectionIDs</name>
            <dataType>string</dataType>
            <defaultValue>0</defaultValue>
        </stateVariable>
    </serviceStateTable>
</scpd>
";

    string UpnpMSMediaReceiverRegistrarXml => $@"
<scpd xmlns=""urn:schemas-upnp-org:service-1-0"">
    <specVersion>
        <major>1</major>
        <minor>0</minor>
    </specVersion>
    <actionList>
        <action>
            <name>IsAuthorized</name>
            <argumentList>
                <argument>
                    <name>DeviceID</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_DeviceID</relatedStateVariable>
                </argument>
                <argument>
                    <name>Result</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_Result</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
        <action>
            <name>RegisterDevice</name>
            <argumentList>
                <argument>
                    <name>RegistrationReqMsg</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_RegistrationReqMsg</relatedStateVariable>
                </argument>
                <argument>
                    <name>RegistrationRespMsg</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_RegistrationRespMsg</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
        <action>
            <name>IsValidated</name>
            <argumentList>
                <argument>
                    <name>DeviceID</name>
                    <direction>in</direction>
                    <relatedStateVariable>A_ARG_TYPE_DeviceID</relatedStateVariable>
                </argument>
                <argument>
                    <name>Result</name>
                    <direction>out</direction>
                    <relatedStateVariable>A_ARG_TYPE_Result</relatedStateVariable>
                </argument>
            </argumentList>
        </action>
    </actionList>
    <serviceStateTable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_DeviceID</name>
            <dataType>string</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_Result</name>
            <dataType>int</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_RegistrationReqMsg</name>
            <dataType>bin.base64</dataType>
        </stateVariable>
        <stateVariable sendEvents=""no"">
            <name>A_ARG_TYPE_RegistrationRespMsg</name>
            <dataType>bin.base64</dataType>
        </stateVariable>
        <stateVariable sendEvents=""yes"">
            <name>AuthorizationGrantedUpdateID</name>
            <dataType>ui4</dataType>
        </stateVariable>
        <stateVariable sendEvents=""yes"">
            <name>AuthorizationDeniedUpdateID</name>
            <dataType>ui4</dataType>
        </stateVariable>
        <stateVariable sendEvents=""yes"">
            <name>ValidationSucceededUpdateID</name>
            <dataType>ui4</dataType>
        </stateVariable>
        <stateVariable sendEvents=""yes"">
            <name>ValidationRevokedUpdateID</name>
            <dataType>ui4</dataType>
        </stateVariable>
    </serviceStateTable>
</scpd>
";
}


internal class WebStartup
{
    public void Configure(IApplicationBuilder app, WebServer server)
    {
        app.UseWebSockets();
        app.UseWebServer(server);
    }
}

internal static class WebExtensions
{
    internal static IApplicationBuilder UseWebServer(this IApplicationBuilder app, WebServer server) =>
        app.Use(async (HttpContext c, Func<Task> _) =>
        {
            try
            {
                await server.ProcessHttpContext(c);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        });
}
