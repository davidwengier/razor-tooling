﻿// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line hidden
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
using Microsoft.AspNetCore.Components.Web;

#line default
#line hidden
#nullable disable
    public partial class TestComponent : global::Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            __builder.OpenElement(0, "button");
            __builder.AddAttribute(1, "onclick", global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create<global::Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, 
#nullable restore
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
                  () => Foo = false

#line default
#line hidden
#nullable disable
            ));
            __builder.AddEventPreventDefaultAttribute(2, "onfocus", 
#nullable restore
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
                                                              true

#line default
#line hidden
#nullable disable
            );
            __builder.AddEventStopPropagationAttribute(3, "onclick", 
#nullable restore
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
                                                                                              Foo

#line default
#line hidden
#nullable disable
            );
            __builder.AddEventStopPropagationAttribute(4, "onfocus", 
#nullable restore
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
                                                                                                                             false

#line default
#line hidden
#nullable disable
            );
            __builder.AddContent(5, "Click Me");
            __builder.CloseElement();
        }
        #pragma warning restore 1998
#nullable restore
#line 3 "x:\dir\subdir\Test\TestComponent.cshtml"
       
    bool Foo { get; set; }

#line default
#line hidden
#nullable disable
    }
}
#pragma warning restore 1591
