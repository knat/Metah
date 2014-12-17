//Program.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;//for XmlReader & XmlWriter
using System.Xml.Linq;//for XName & XNamespace
using X = Metah.X;
using Metah.X.Extensions;

namespace Example.ProjectA {
    partial class Phone {
        private Phone() { }
        public Phone(string value, string phoneType = null) {
            Value = value;
            if (phoneType != null) EnsureAttributeSet().PhoneType_Value = phoneType;
        }
    }
    partial class Phones {
        private Phones() { }
        public Phones(params Phone[] phones) {
            var phoneList = EnsureComplexChild().Ensure_Phones();
            foreach (var phone in phones)
                phoneList.CreateAndAddItem().Type = phone;
        }
    }
    partial class Address {
        private Address() { }
        public Address(string country, string state, string city, string address, string zipCode) {
            var attset = EnsureComplexChild().Ensure_Choice().Ensure_Normal().EnsureAttributeSet();
            attset.Country_Value = country;
            if (state != null) attset.State_Value = state;
            attset.City_Value = city;
            attset.Address_Value = address;
            attset.ZipCode_Value = zipCode;
        }
        public Address(decimal longitude, decimal latitude) {
            var attset = EnsureComplexChild().Ensure_Choice().Ensure_Geography().EnsureAttributeSet();
            attset.Longitude_Value = longitude;
            attset.Latitude_Value = latitude;
        }
        public override string ToString() {
            var normal = ComplexChild.Choice.Normal;
            if (normal != null) {
                var nattset = normal.AttributeSet;
                return nattset.Country_Value + ", " + nattset.City_Value + ", " + nattset.Address_Value;
            }
            var gattset = ComplexChild.Choice.Geography.AttributeSet;
            return "(" + gattset.Longitude_Value + ", " + gattset.Latitude_Value + ")";
        }
    }
    partial class Customer {
        private Customer() { }
        public Customer(string name, string email, Phones phones, Address address) {
            var attset = EnsureAttributeSet();
            attset.Name_Value = name;
            attset.Email_Value = email;
            attset.RegistrationDate_Value = DateTime.Now;
            var cc = EnsureComplexChild();
            cc.Ensure_Phones().Type = phones;
            cc.Ensure_Address().Type = address;
        }
        public override string ToString() {
            var attset = AttributeSet;
            var cc = ComplexChild;
            return string.Format("Name: {0}, Email: {1}, Address: {2}", attset.Name_Value, attset.Email_Value, cc.Address.Type);
        }
    }
    class Program {
        static void Main(string[] args) {
            var customer = new Customer("Tank", "someone@example.com",
                new Phones(new Phone("1234567", PhoneType.Home), new Phone("7654321")),
                new Address("China", "Sichuan", "Suining", "somewhere", "629000"));
            var ctx = new X.Context();
            if (!customer.TryValidate(ctx)) {
                Display(ctx);
                return;
            }
            Console.WriteLine(customer);
            customer.EnsureComplexChild().Ensure_Address().Type = new Address(105.123M, 30.345M);
            Console.WriteLine(customer);
            var customerElement = new Customer_ElementClass { Type = customer };
            using (var writer = XmlWriter.Create(@"d:\customer.xml", new XmlWriterSettings { Indent = true }))
                customerElement.Save(writer);
            ctx.Reset();
            using (var reader = XmlReader.Create(@"d:\customer.xml")) {
                Customer_ElementClass customerElement2;
                if (!Customer_ElementClass.TryLoadAndValidate(reader, ctx, out customerElement2)) {
                    Display(ctx);
                    return;
                }
                Console.WriteLine(customerElement2.Type);
            }
            //
            ProjectB.Test.Run();
        }
        static void Display(X.Context ctx) {
            foreach (var diag in ctx.Diagnostics)
                Console.WriteLine(diag);
        }
    }
}
