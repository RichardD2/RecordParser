﻿using FluentAssertions;
using RecordParser.Parsers;
using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace RecordParser.Test
{
    public class VariableLengthReaderBuilderTest
    {
        [Fact]
        public void Given_value_using_standard_format_should_parse_without_extra_configuration()
        {
            var reader = new VariableLengthReaderBuilder<(string Name, DateTime Birthday, decimal Money, Color Color)>()
                .Map(x => x.Name, indexColumn: 0)
                .Map(x => x.Birthday, 1)
                .Map(x => x.Money, 2)
                .Map(x => x.Color, 3)
                .BuildForUnitTest();

            var result = reader.Parse("foo bar baz ; 2020.05.23 ; 0123.45; LightBlue");

            result.Should().BeEquivalentTo((Name: "foo bar baz",
                                            Birthday: new DateTime(2020, 05, 23),
                                            Money: 123.45M,
                                            Color: Color.LightBlue));
        }

        [Fact]
        public void Given_types_with_custom_format_should_allow_define_default_parser_for_type()
        {
            var reader = new VariableLengthReaderBuilder<(decimal Debit, decimal Balance, DateTime Date)>()
                .Map(x => x.Balance, 0)
                .Map(x => x.Date, 1)
                .Map(x => x.Debit, 2)
                .DefaultTypeConvert(value => decimal.Parse(value) / 100)
                .DefaultTypeConvert(value => DateTime.ParseExact(value, "ddMMyyyy", null))
                .BuildForUnitTest();

            var result = reader.Parse("012345678901 ; 23052020 ; 012345");

            result.Should().BeEquivalentTo((Debit: 0123.45M,
                                            Balance: 0123456789.01M,
                                            Date: new DateTime(2020, 05, 23)));
        }

        [Fact]
        public void Given_members_with_custom_format_should_use_custom_parser()
        {
            var reader = new VariableLengthReaderBuilder<(string Name, DateTime Birthday, decimal Money, string Nickname)>()
                .Map(x => x.Name, 0, value => value.ToUpper())
                .Map(x => x.Birthday, 1, value => DateTime.ParseExact(value, "ddMMyyyy", null))
                .Map(x => x.Money, 2)
                .Map(x => x.Nickname, 3, value => value.Slice(0, 4).ToString())
                .BuildForUnitTest();

            var result = reader.Parse("foo bar baz ; 23052020 ; 012345 ; nickname");

            result.Should().BeEquivalentTo((Name: "FOO BAR BAZ",
                                            Birthday: new DateTime(2020, 05, 23),
                                            Money: 12345M,
                                            Nickname: "nick"));
        }

        [Fact]
        public void Given_specified_custom_parser_for_member_should_have_priority_over_custom_parser_for_type()
        {
            var reader = new VariableLengthReaderBuilder<(int Age, int MotherAge, int FatherAge)>()
                .Map(x => x.Age, 0, value => int.Parse(value) * 2)
                .Map(x => x.MotherAge, 1)
                .Map(x => x.FatherAge, 2)
                .DefaultTypeConvert(value => int.Parse(value) + 2)
                .BuildForUnitTest();

            var result = reader.Parse(" 15 ; 40 ; 50 ");

            result.Should().BeEquivalentTo((Age: 30,
                                            MotherAge: 42,
                                            FatherAge: 52));
        }

        [Fact]
        public void Custom_format_configurations_can_be_simplified_with_user_defined_extension_methods()
        {
            var reader = new VariableLengthReaderBuilder<(string Name, decimal Balance, DateTime Date)>()
                .Map(x => x.Balance, 0)
                .Map(x => x.Name, 2)
                .MyMap(x => x.Date, 1, format: "ddMMyyyy")
                .MyBuild();

            var result = reader.Parse("012345678.901 ; 23052020 ; FOOBAR ");

            result.Should().BeEquivalentTo((Name: "foobar",
                                            Balance: 012345678.901M,
                                            Date: new DateTime(2020, 05, 23)));
        }

        [Fact]
        public void Given_trim_is_enabled_should_remove_whitespace_from_both_sides_of_string()
        {
            var reader = new VariableLengthReaderBuilder<(string Foo, string Bar, string Baz)>()
                .Map(x => x.Foo, 0)
                .Map(x => x.Bar, 1)
                .Map(x => x.Baz, 2)
                .BuildForUnitTest();

            var result = reader.Parse(" foo ; bar ; baz ");

            result.Should().BeEquivalentTo((Foo: "foo",
                                            Bar: "bar",
                                            Baz: "baz"));
        }

        [Fact]
        public void Given_invalid_record_called_with_try_parse_should_not_throw()
        {
            var reader = new VariableLengthReaderBuilder<(string Name, DateTime Birthday)>()
                .Map(x => x.Name, 0)
                .Map(x => x.Birthday, 1)
                .BuildForUnitTest();

            var parsed = reader.TryParse(" foo ; datehere", out var result);

            parsed.Should().BeFalse();
            result.Should().Be(default);
        }

        [Fact]
        public void Given_valid_record_called_with_try_parse_should_set_out_parameter_with_result()
        {
            var reader = new VariableLengthReaderBuilder<(string Name, DateTime Birthday, decimal Money, Color Color)>()
                .Map(x => x.Name, indexColumn: 0)
                .Map(x => x.Birthday, 1)
                .Map(x => x.Money, 2)
                .Map(x => x.Color, 3)
                .BuildForUnitTest();

            var parsed = reader.TryParse("foo bar baz ; 2020.05.23 ; 0123.45; LightBlue", out var result);

            parsed.Should().BeTrue();
            result.Should().BeEquivalentTo((Name: "foo bar baz",
                                            Birthday: new DateTime(2020, 05, 23),
                                            Money: 123.45M,
                                            Color: Color.LightBlue));
        }

        [Fact]
        public void Given_nested_mapped_property_should_create_nested_instance_to_parse()
        {
            var reader = new VariableLengthReaderBuilder<Person>()
                .Map(x => x.BirthDay, 0)
                .Map(x => x.Name, 1)
                .Map(x => x.Mother.BirthDay, 2)
                .Map(x => x.Mother.Name, 3)
                .BuildForUnitTest();

            var result = reader.Parse("2020.05.23 ; son name ; 1980.01.15 ; mother name");

            result.Should().BeEquivalentTo(new Person
            {
                BirthDay = new DateTime(2020, 05, 23),
                Name = "son name",
                Mother = new Person
                {
                    BirthDay = new DateTime(1980, 01, 15),
                    Name = "mother name",
                }
            });
        }

        [Theory]
        [InlineData("pt-BR")]
        [InlineData("en-US")]
        [InlineData("fr-FR")]
        [InlineData("ru-RU")]
        [InlineData("es-ES")]
        public void Builder_should_use_passed_cultureinfo_to_parse_record(string cultureName)
        {
            var culture = new CultureInfo(cultureName);

            var expected = (Name: "foo bar baz",
                            Birthday: new DateTime(2020, 05, 23),
                            Money: 123.45M,
                            Color: Color.LightBlue);

            var reader = new VariableLengthReaderBuilder<(string Name, DateTime Birthday, decimal Money, Color Color)>()
                 .Map(x => x.Name, 0)
                 .Map(x => x.Birthday, 1)
                 .Map(x => x.Money, 2)
                 .Map(x => x.Color, 3)
                 .Build(";", culture);

            var values = new[]
            {
                expected.Name.ToString(culture),
                expected.Birthday.ToString(culture),
                expected.Money.ToString(culture),
                expected.Color.ToString(),
            };

            var line = string.Join(';', values.Select(x => $"  {x}  "));

            var result = reader.Parse(line);

            result.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData("pt-BR")]
        [InlineData("en-US")]
        [InlineData("fr-FR")]
        [InlineData("ru-RU")]
        [InlineData("es-ES")]
        public void Registered_primitives_types_should_have_default_converters_which_uses_current_cultureinfo(string cultureName)
        {
            var expected = new AllType
            {
                Str = "Foo Bar",
                Char = 'z',

                Byte = 42,
                SByte = -43,

                Double = -1.58D,
                Float = 1.46F,

                Int = -6,
                UInt = 7,

                Long = -3,
                ULong = 45,

                Short = -2,
                UShort = 8,

                Guid = new Guid("e808927a-48f9-4402-ab2b-400bf1658169"),
                Date = DateTime.Parse(DateTime.Now.ToString()),
                TimeSpan = DateTime.Now.TimeOfDay,

                Bool = true,
                Decimal = -1.99M,
            };

            var reader = new VariableLengthReaderSequentialBuilder<AllType>()
            .Map(x => x.Str)
            .Map(x => x.Char)

            .Map(x => x.Byte)
            .Map(x => x.SByte)

            .Map(x => x.Double)
            .Map(x => x.Float)

            .Map(x => x.Int)
            .Map(x => x.UInt)

            .Map(x => x.Long)
            .Map(x => x.ULong)

            .Map(x => x.Short)
            .Map(x => x.UShort)

            .Map(x => x.Guid)
            .Map(x => x.Date)
            .Map(x => x.TimeSpan)

            .Map(x => x.Bool)
            .Map(x => x.Decimal)

            .Build(";");

            var values = new object[]
            {
                expected.Str,
                expected.Char,

                expected.Byte,
                expected.SByte,

                expected.Double,
                expected.Float,

                expected.Int,
                expected.UInt,

                expected.Long,
                expected.ULong,

                expected.Short,
                expected.UShort,

                expected.Guid,
                expected.Date,
                expected.TimeSpan,

                expected.Bool,
                expected.Decimal,
            };

            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            var line = string.Join(';', values.Select(x => $"  {x}  "));

            var result = reader.Parse(line);

            result.Should().BeEquivalentTo(expected);
        }

        public enum EmptyEnum
        {

        }

        [Fact]
        public void Parse_enum_same_way_framework()
        {
            var reader = new VariableLengthReaderBuilder<(Color color, bool _)>()
                .Map(x => x.color, 0)
                .BuildForUnitTest();

            // text as is
            reader.Parse("Black").color.Should().Be(Color.Black);

            // text uppercase
            reader.Parse("WHITE").color.Should().Be(Color.White);

            // text lowercase
            reader.Parse("yellow").color.Should().Be(Color.Yellow);

            // numeric value present in enum
            reader.Parse("3").color.Should().Be(Color.LightBlue);

            // numeric value NOT present in enum
            reader.Parse("777").color.Should().Be((Color)777);

            // text NOT present in enum
            Action act = () => reader.Parse("foo");

            act.Should().Throw<ArgumentException>().WithMessage("value foo not present in enum Color");

            // enum without elements
            reader.Parse("777").color.Should().Be((EmptyEnum)777);
        }
    }

    public static class VariableLengthReaderCustomExtensions
    {
        public static IVariableLengthReader<T> BuildForUnitTest<T>(this IVariableLengthReaderBuilder<T> source)
            => source.Build(";", CultureInfo.InvariantCulture);

        public static IVariableLengthReader<T> BuildForUnitTest<T>(this IVariableLengthReaderSequentialBuilder<T> source)
            => source.Build(";", CultureInfo.InvariantCulture);

        public static IVariableLengthReaderBuilder<T> MyMap<T>(
            this IVariableLengthReaderBuilder<T> source,
            Expression<Func<T, DateTime>> ex, int startIndex,
            string format)
        {
            return source.Map(ex, startIndex, value => DateTime.ParseExact(value, format, null));
        }

        public static IVariableLengthReader<T> MyBuild<T>(this IVariableLengthReaderBuilder<T> source)
        {
            return source.DefaultTypeConvert(value => value.ToLower())
                         .BuildForUnitTest();
        }
    }
}
