﻿// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line default
    using global::System;
    using global::System.Collections.Generic;
    using global::System.Linq;
    using global::System.Threading.Tasks;
    using global::Microsoft.AspNetCore.Components;
    #line default
    #line hidden
    #nullable restore
    public partial class TestComponent : global::Microsoft.AspNetCore.Components.ComponentBase
    #nullable disable
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            __builder.OpenComponent<global::Test.MyComponent<string>>(0);
            __builder.AddComponentParameter(1, nameof(global::Test.MyComponent<string>.
#nullable restore
#line (1,27)-(1,31) "x:\dir\subdir\Test\TestComponent.cshtml"
Item

#line default
#line hidden
#nullable disable
            ), global::Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<string>(
#nullable restore
#line (1,35)-(1,39) "x:\dir\subdir\Test\TestComponent.cshtml"
"hi"

#line default
#line hidden
#nullable disable
            ));
            __builder.AddAttribute(2, "ChildContent", (global::Microsoft.AspNetCore.Components.RenderFragment<string>)((context) => (__builder2) => {
                __builder2.OpenElement(3, "div");
                __builder2.AddContent(4, 
#nullable restore
#line (2,9)-(2,26) "x:\dir\subdir\Test\TestComponent.cshtml"
context.ToLower()

#line default
#line hidden
#nullable disable
                );
                __builder2.CloseElement();
            }
            ));
            __builder.CloseComponent();
        }
        #pragma warning restore 1998
    }
}
#pragma warning restore 1591
